using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Constants;
using BatBot.Server.Dtos;
using BatBot.Server.Extensions;
using BatBot.Server.Functions;
using BatBot.Server.Models;
using BatBot.Server.Models.Graph;
using BatBot.Server.Types;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Rationals;

namespace BatBot.Server.Services
{
    public sealed class TransactionProcessorService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly SettingsOptions _settingsOptions;
        private readonly MessagingService _messagingService;
        private readonly BackoffService _backoffService;
        private readonly BlockchainService _blockchainService;
        private readonly GraphService _graphService;
        private readonly TokenInfoService _tokenInfoService;
        private readonly TransactionWaitService _transactionWaitService;

        private static string _factoryAddress;
        private static readonly Dictionary<(string, string), string> PairAddresses = new Dictionary<(string, string), string>();

        public TransactionProcessorService(
            IOptionsFactory<BatBotOptions> batBotOptionsFactory,
            IOptionsFactory<SettingsOptions> settingsOptionsFactory,
            MessagingService messagingService,
            BackoffService backoffService, BlockchainService blockchainService,
            GraphService graphService,
            TokenInfoService tokenInfoService,
            TransactionWaitService transactionWaitService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _settingsOptions = settingsOptionsFactory.Create(Options.DefaultName);
            _messagingService = messagingService;
            _backoffService = backoffService;
            _blockchainService = blockchainService;
            _graphService = graphService;
            _tokenInfoService = tokenInfoService;
            _transactionWaitService = transactionWaitService;
        }

        public async Task Process(Swap swap, CancellationToken cancellationToken)
        {
            // Ignore transactions that are beyond their deadline, or outside of the configured range.
            if (swap.Deadline < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;

            await _backoffService.Throttle(async () =>
            {
                _transactionWaitService.AddTransaction(swap.TransactionHash);

                var prepWeb3 = new Web3(new Account(_batBotOptions.PrepPrivateKey), _batBotOptions.BlockchainEndpointHttpUrl);

                var detectedMessage = $"🔎 Detected transaction: {_messagingService.GetEtherscanUrl($"tx/{swap.TransactionHash}")}";
                await SendPreFrontRunMessage(detectedMessage, MessageTier.Double);

                var tokenIn = await _tokenInfoService.GetToken(prepWeb3, swap.Path.First(), cancellationToken);
                var tokenOut = await _tokenInfoService.GetToken(prepWeb3, swap.Path.Last(), cancellationToken);

                var swapMessage = $"💱 Swap {Web3.Convert.FromWei(swap.AmountToSend)} {tokenIn.Symbol} for {Web3.Convert.FromWei(swap.AmountOutMin, tokenOut.Decimals)} {tokenOut.Symbol} with gas price {Web3.Convert.FromWei(swap.GasPrice.GetValueOrDefault(), UnitConversion.EthUnit.Gwei)} Gwei";
                await SendPreFrontRunMessage(swapMessage, MessageTier.End | MessageTier.Single);

                #region Validation

                if (!_settingsOptions.YoloMode)
                {
                    // Ignore transactions that are outside of the configured range.
                    if (!swap.AmountToSend.Between(Web3.Convert.ToWei(_settingsOptions.MinimumTargetSwapEth), Web3.Convert.ToWei(_settingsOptions.MaximumTargetSwapEth)))
                    {
                        await SendPreFrontRunMessage("⛔ Amount to send outside of the configured range", MessageTier.End);
                        return;
                    }

                    // Ignore transactions with too high gas price.
                    if (swap.GasPrice > Web3.Convert.ToWei(_settingsOptions.MaximumGasPrice, UnitConversion.EthUnit.Gwei))
                    {
                        await SendPreFrontRunMessage("⛔ Gas price higher than the configured limit", MessageTier.End);
                        return;
                    }

                    _factoryAddress ??= await _blockchainService.ContractQuery<FactoryFunction, string>(prepWeb3, _batBotOptions.ContractAddress);

                    // No need to perform the following check on a testnet.
                    if (_batBotOptions.Network == BatBotOptions.Mainnet)
                    {
                        var pairSwap = await GetPair(prepWeb3, (tokenIn.Address, tokenOut.Address));
                        var pairUsdt = await GetPair(prepWeb3, (tokenIn.Address, _batBotOptions.BaseTokenAddress));

                        if (pairSwap is null)
                        {
                            await SendPreFrontRunMessage("⛔ Unable to obtain pair contract for this swap", MessageTier.End);
                            return;
                        }

                        // Check that the market cap of the output token is above a minimum amount to protect against possible scam coins.
                        var marketCap = pairUsdt.GetTokenPrice(_batBotOptions.BaseTokenAddress) * pairSwap.GetTokenPrice(tokenIn.Address) * tokenOut.TotalSupply;

                        if (marketCap < (Rational)_settingsOptions.MinimumMarketCap)
                        {
                            await SendPreFrontRunMessage($"⛔ Market cap (${marketCap.WholePart}) too low", MessageTier.End);
                            return;
                        }

                        // Check that the pair contract wasn't created too recently to protect against possible scam coins.
                        if (pairSwap.CreatedValue > DateTime.UtcNow.AddDays(_settingsOptions.MinimumPairContractAge))
                        {
                            await SendPreFrontRunMessage($"⛔ Pair contract created too recently ({pairSwap.CreatedValue:g})", MessageTier.End);
                            return;
                        }
                    }
                }

                #endregion

                try
                {
                    #region Preparation

                    var frontRunAmountIn = swap.AmountToSend;
                    BigInteger frontRunAmountOutMin;
                    BigInteger targetSwapAmountOut;

                    do
                    {
                        // Find the optimal amount to front run with by inferring the effect of slippage from iterative queries to the smart contract.
                        frontRunAmountOutMin = await GetAmountOut(prepWeb3, frontRunAmountIn, swap.Path);
                        targetSwapAmountOut = await GetAmountOut(prepWeb3, swap.AmountToSend + frontRunAmountIn, swap.Path) - frontRunAmountOutMin;

                        var frontRunAmountInMin = Web3.Convert.ToWei(_settingsOptions.MinimumFrontRunEth);
                        var frontRunAmountInMax = Web3.Convert.ToWei(_settingsOptions.MaximumFrontRunEth);

                        if (_settingsOptions.YoloMode) break;

                        var targetSwapAmountOutAboveMin = targetSwapAmountOut > (swap.AmountOutMin * (Rational)(1 + _settingsOptions.TargetSwapAmountOutToleranceMin)).WholePart;
                        var targetSwapAmountOutBelowMax = targetSwapAmountOut < (swap.AmountOutMin * (Rational)(1 + _settingsOptions.TargetSwapAmountOutToleranceMax)).WholePart;
                        if (targetSwapAmountOutAboveMin && targetSwapAmountOutBelowMax) break;

                        if (targetSwapAmountOutBelowMax && frontRunAmountIn == frontRunAmountInMin)
                        {
                            await SendPreFrontRunMessage("⛔ Amount in is too low to front run", MessageTier.End);
                            return;
                        }

                        if (targetSwapAmountOutAboveMin && frontRunAmountIn == frontRunAmountInMax)
                        {
                            await SendPreFrontRunMessage("💯 Amount in to front run is at configured maximum", MessageTier.Single);
                            break;
                        }

                        // Adjust front run amount iteratively to get as close as possible to the amount that won't cause the target swap to fail, but keep within configured bounds.
                        frontRunAmountIn = BigInteger.Max(BigInteger.Min((frontRunAmountIn * (Rational)(targetSwapAmountOutBelowMax ? 0.5 : 1.5)).WholePart, frontRunAmountInMax), frontRunAmountInMin);
                    } while (true);

                    // Calculate the slippage as the difference between the amounts out and amounts in ratios, then compare to the tolerance.
                    var slippage = new Rational(frontRunAmountOutMin) / new Rational(targetSwapAmountOut) / (new Rational(frontRunAmountIn) / new Rational(swap.AmountToSend)) - 1;
                    if (!_settingsOptions.YoloMode && slippage < (Rational)_settingsOptions.SlippageTolerance)
                    {
                        await SendPreFrontRunMessage($"⛔ Slippage ({(double)slippage:P}) too tight to front run", MessageTier.End);
                        return;
                    }

                    // Prepare the front run message, and set with a higher gas price than the target swap.
                    var frontRunBuyMessage = new SwapExactEthForTokensFunction
                    {
                        AmountToSend = frontRunAmountIn,
                        AmountOutMin = (frontRunAmountOutMin * (Rational)0.999).WholePart,
                        Path = swap.Path,
                        To = _batBotOptions.FrontRunWalletAddress,
                        Deadline = swap.Deadline,
                        GasPrice = (swap.GasPrice.GetValueOrDefault() * (Rational)_settingsOptions.FrontRunGasPriceFactor).WholePart,
                        Gas = swap.Gas
                    };

                    // Prepare the sell message for later selling the front run tokens that will be bought.
                    var frontRunSellMessage = new SwapExactTokensForEthFunction
                    {
                        AmountOutMin = 0,
                        Path = Enumerable.Reverse(frontRunBuyMessage.Path).ToList(),
                        To = _batBotOptions.FrontRunWalletAddress,
                        Deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds()
                    };

                    #endregion

                    #region Prechecks

                    // Check that the front run swap will in fact succeed before front running.
                    if (await GetAmountOut(prepWeb3, frontRunBuyMessage.AmountToSend, frontRunBuyMessage.Path) < frontRunBuyMessage.AmountOutMin)
                    {
                        await SendPreFrontRunMessage("‼️ Aborting (not front running) due to insufficient output amount on front run swap", MessageTier.End);
                        return;
                    }

                    #endregion

                    #region Front run

                    if (!_settingsOptions.ShowDiscardedSwapsOutput)
                    {
                        await _messagingService.SendTxMessage(detectedMessage, MessageTier.Double);
                        await _messagingService.SendTxMessage(swapMessage, MessageTier.End | MessageTier.Single);
                    }

                    // Switch address to front run private key (if different) to guard against front run detection.
                    var frontRunWeb3 = new Web3(new Account(_batBotOptions.FrontRunPrivateKey), _batBotOptions.BlockchainEndpointHttpUrl);

                    var preBalance = await GetBalance(frontRunWeb3);

                    // Now attempt to front run the target swap with a faster swap.
                    await _messagingService.SendTxMessage($"🏃 Front running {Web3.Convert.FromWei(frontRunBuyMessage.AmountToSend)} {tokenIn.Symbol} with gas price {Web3.Convert.FromWei(frontRunBuyMessage.GasPrice.GetValueOrDefault(), UnitConversion.EthUnit.Gwei)} gwei", MessageTier.Double);
                    var frontRunBuyReceipt = await _blockchainService.ContractSendRequestAndWaitForReceipt(frontRunWeb3, _batBotOptions.ContractAddress, frontRunBuyMessage, false);
                    await _messagingService.SendTxMessage($"#️⃣ Front run transaction: {_messagingService.GetEtherscanUrl($"tx/{frontRunBuyReceipt.TransactionHash}")}");

                    if (frontRunBuyReceipt.Failed())
                    {
                        await _messagingService.SendTxMessage("❌ Failed", MessageTier.End);
                        return;
                    }

                    // The last swap event contains the output amount.
                    var frontRunSwapEvent = frontRunBuyReceipt.DecodeAllEvents<SwapEventDTO>().Last().Event;
                    var amountBought = frontRunSwapEvent.Amount0In == frontRunBuyMessage.AmountToSend ? frontRunSwapEvent.Amount1Out : frontRunSwapEvent.Amount0Out;

                    await _messagingService.SendTxMessage($"✅ Successfully bought {Web3.Convert.FromWei(amountBought, tokenOut.Decimals)} {tokenOut.Symbol}", MessageTier.End | MessageTier.Single);

                    #endregion

                    #region Pre-sell

                    var waitTask = Task.Run(async () =>
                    {
                        // Wait for original (target's) swap to be mined.
                        if (!_transactionWaitService.WaitForTransaction(swap.TransactionHash, swap.Source, cancellationToken).Result && !_settingsOptions.SellOnFailedTargetSwap)
                        {
                            await _messagingService.SendTxMessage("‼️ Aborting (not selling) due to target swap failing (as configured)", MessageTier.End);
                            return false;
                        }

                        return true;
                    }, cancellationToken);

                    var approveTask = Task.Run(async () =>
                    {
                        // Ensure that the tokens bought are approved for selling before purchasing.
                        await _messagingService.SendTxMessage($"❓ Checking spender {_batBotOptions.ContractAddress} allowed");
                        var allowed = await _blockchainService.ContractQuery<AllowanceFunction, BigInteger>(frontRunWeb3, frontRunSellMessage.Path.First(), new AllowanceFunction
                        {
                            Owner = _batBotOptions.FrontRunWalletAddress,
                            Spender = _batBotOptions.ContractAddress
                        });

                        if (allowed.IsZero)
                        {
                            await _messagingService.SendTxMessage($"🤝 Approving spender {_batBotOptions.ContractAddress}");
                            var approveReceipt = await _blockchainService.ContractSendRequestAndWaitForReceipt(frontRunWeb3, frontRunSellMessage.Path.First(), new ApproveFunction
                            {
                                Spender = _batBotOptions.ContractAddress,
                                Value = (BigInteger.One << 256) - 1
                            });

                            if (approveReceipt.Failed())
                            {
                                await _messagingService.SendTxMessage("❌ Failed", MessageTier.End);
                                return false;
                            }
                        }

                        return true;
                    }, cancellationToken);

                    if ((await Task.WhenAll(approveTask, waitTask)).Any(r => !r)) return;

                    #endregion

                    #region Sell

                    frontRunSellMessage.AmountIn = amountBought;
                    await _messagingService.SendTxMessage($"🛒 Selling {Web3.Convert.FromWei(frontRunSellMessage.AmountIn, tokenOut.Decimals)} {tokenOut.Symbol}", MessageTier.Double);
                    TransactionReceipt frontRunSellReceipt;

                    try
                    {
                        // Sell the front run tokens bought earlier.
                        frontRunSellReceipt = await _blockchainService.ContractSendRequestAndWaitForReceipt(frontRunWeb3, _batBotOptions.ContractAddress, frontRunSellMessage);
                    }
                    catch (SmartContractRevertException e)
                    {
                        await _messagingService.SendTxMessage($"❌ Failed with error '{e.Message}'", MessageTier.End);
                        return;
                    }

                    await _messagingService.SendTxMessage($"#️⃣ Sell transaction: {_messagingService.GetEtherscanUrl($"tx/{frontRunSellReceipt.TransactionHash}")}", MessageTier.Double);

                    if (frontRunSellReceipt.Failed())
                    {
                        await _messagingService.SendTxMessage("❌ Failed", MessageTier.End);
                        return;
                    }

                    var sellSwapEvent = frontRunSellReceipt.DecodeAllEvents<SwapEventDTO>().Single().Event;
                    var ethBought = sellSwapEvent.Amount0In == frontRunSellMessage.AmountIn ? sellSwapEvent.Amount1Out : sellSwapEvent.Amount0Out;

                    await _messagingService.SendTxMessage($"✅ Successfully sold for {Web3.Convert.FromWei(ethBought)} {tokenIn.Symbol}", MessageTier.End | MessageTier.Single);

                    var postBalance = await GetBalance(frontRunWeb3);
                    var change = postBalance - preBalance;
                    await _messagingService.SendTxMessage($"{(change > 0 ? "🚀 Profit: " : "🔻 Loss: ")} {change}", MessageTier.End);

                    #endregion
                }
                catch (Exception e) when (e is RpcResponseException || e is SmartContractRevertException)
                {
                    await _messagingService.SendTxMessage($"❌ Failed with error '{e.Message}'", MessageTier.End);
                }

                #region Local functions

                async Task<PairResponse.PairType> GetPair(IWeb3 web3, (string TokenA, string TokenB) tokens)
                {
                    if (!PairAddresses.TryGetValue(tokens, out var pairAddress))
                    {
                        pairAddress = (await _blockchainService.ContractQuery<GetPairFunction, string>(web3, _factoryAddress, new GetPairFunction {TokenA = tokens.TokenA, TokenB = tokens.TokenB})).ToLowerInvariant();
                        PairAddresses.Add(tokens, pairAddress);
                    }

                    return pairAddress != Uniswap.InvalidAddress ? (await _graphService.SendQuery<PairResponse>(new {id = pairAddress}, cancellationToken: cancellationToken)).Pair : null;
                }

                async Task SendPreFrontRunMessage(string message, MessageTier messageTier)
                {
                    if (_settingsOptions.ShowDiscardedSwapsOutput)
                    {
                        await _messagingService.SendTxMessage(message, messageTier);
                    }
                    else
                    {
                        await _messagingService.SendLogMessage(message);
                    }
                }

                async Task<BigInteger> GetAmountOut(IWeb3 web3, BigInteger amountIn, List<string> path) => (await _blockchainService.ContractQuery<GetAmountsOutFunction, List<BigInteger>>(web3, _batBotOptions.ContractAddress, new GetAmountsOutFunction
                {
                    AmountIn = amountIn,
                    Path = path
                })).Last();

                async Task<decimal> GetBalance(IWeb3 web3)
                {
                    var balance = Web3.Convert.FromWei(await web3.Eth.GetBalance.SendRequestAsync(_batBotOptions.FrontRunWalletAddress));
                    await _messagingService.SendTxMessage($"💰 Current ETH balance: {balance}");
                    return balance;
                }

                #endregion
            });
        }
    }
}
