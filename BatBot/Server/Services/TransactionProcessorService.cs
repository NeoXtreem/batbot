using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using BatBot.Server.Constants;
using BatBot.Server.Dtos;
using BatBot.Server.Dtos.Graph;
using BatBot.Server.Extensions;
using BatBot.Server.Functions;
using BatBot.Server.Helpers;
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
using Swap = BatBot.Server.Models.Swap;

namespace BatBot.Server.Services
{
    public sealed class TransactionProcessorService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly SettingsOptions _settingsOptions;
        private readonly IMapper _mapper;
        private readonly MessagingService _messagingService;
        private readonly BackoffService _backoffService;
        private readonly TransactionWaitService _transactionWaitService;
        private readonly PairInfoService _pairInfoService;
        private readonly GraphService _graphService;
        private readonly EthereumService _ethereumService;
        private readonly SmartContractService _smartContractService;

        private readonly Dictionary<string, bool> _pairWhitelist = new Dictionary<string, bool>();

        public TransactionProcessorService(
            IOptionsFactory<BatBotOptions> batBotOptionsFactory,
            IOptionsFactory<SettingsOptions> settingsOptionsFactory,
            IMapper mapper,
            MessagingService messagingService,
            BackoffService backoffService,
            TransactionWaitService transactionWaitService,
            PairInfoService pairInfoService,
            GraphService graphService,
            EthereumService ethereumService,
            SmartContractService smartContractService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _settingsOptions = settingsOptionsFactory.Create(Options.DefaultName);
            _mapper = mapper;
            _messagingService = messagingService;
            _backoffService = backoffService;
            _transactionWaitService = transactionWaitService;
            _pairInfoService = pairInfoService;
            _graphService = graphService;
            _ethereumService = ethereumService;
            _smartContractService = smartContractService;
        }

        public async Task Process(Swap swap, CancellationToken cancellationToken)
        {
            // Ignore transactions that are beyond their deadline, or outside of the configured range.
            if (swap.Deadline < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;

            await _backoffService.Throttle(async () =>
            {
                using var waiter = _transactionWaitService.GetWaiter(swap.TransactionHash);
                var prepWeb3 = new Web3(new Account(_batBotOptions.PrepPrivateKey), _batBotOptions.BlockchainEndpointHttpUrl);

                var detectedMessage = $"🔎 Detected transaction: {_messagingService.GetEtherscanUrl($"tx/{swap.TransactionHash}")}";
                await SendPreFrontRunMessage(detectedMessage, MessageTier.Double);

                var pair = await _pairInfoService.GetPair(prepWeb3, (swap.Path.First(), swap.Path.Last()), cancellationToken);

                if (pair is null)
                {
                    await SendPreFrontRunMessage("⛔ Unable to obtain pair contract for this swap", MessageTier.End);
                    return;
                }

                var tokenIn = pair.GetToken(swap.Path.First());
                var tokenOut = pair.GetToken(swap.Path.Last());

                var swapMessage = $"💱 Swap {Web3.Convert.FromWei(swap.AmountIn)} {tokenIn.Symbol} for {Web3.Convert.FromWei(swap.AmountOutMin, tokenOut.Decimals)} {tokenOut.Symbol} with gas price {Web3.Convert.FromWei(swap.GasPrice.GetValueOrDefault(), UnitConversion.EthUnit.Gwei)} Gwei";
                await SendPreFrontRunMessage(swapMessage, MessageTier.End | MessageTier.Single);

                try
                {
                    #region Validation

                    if (!_settingsOptions.YoloMode)
                    {
                        if (_pairWhitelist.TryGetValue(pair.Id, out var allowed) && !allowed)
                        {
                            await SendPreFrontRunMessage("⛔ This token is blacklisted from a previous validation", MessageTier.End);
                            return;
                        }

                        var swapChecks = new List<Func<Task<(bool, string)>>>
                        {
                            // Ignore transactions that are outside of the configured ETH range.
                            async () => (await Task.Run(() => swap.AmountIn.Between(Web3.Convert.ToWei(_settingsOptions.MinimumTargetSwapEth), Web3.Convert.ToWei(_settingsOptions.MaximumTargetSwapEth)), cancellationToken), "Amount to send outside of the configured range"),
                            // Ignore transactions with too high gas price.
                            async () => (await Task.Run(() => swap.GasPrice <= Web3.Convert.ToWei(_settingsOptions.MaximumGasPrice, UnitConversion.EthUnit.Gwei), cancellationToken), "Gas price higher than the configured limit")
                        };

                        var pairChecks = new List<Func<Task<(bool, string)>>>
                        {
                            // Check that the liquidity of the token pair is within the configured range.
                            async () => (await Task.Run(() => pair.ReserveUsd.Between((Rational)_settingsOptions.MinimumLiquidity, (Rational)_settingsOptions.MaximumLiquidity), cancellationToken), $"Liquidity (${pair.ReserveUsd.WholePart:N0}) outside of the configured range"),
                            // Check that the pair contract wasn't created too recently to protect against possible honeypots.
                            async () => (await Task.Run(() => pair.Created.AddDays(_settingsOptions.MinimumPairContractAge) < DateTime.UtcNow, cancellationToken), $"Pair contract created too recently ({pair.Created:g})"),
                            async () =>
                            {
                                // Check that there have been historical swaps selling the output token, to ensure the contract is legitimate.
                                var idName = JsonHelper.GetJsonPropertyName<PairType>(nameof(PairType.Id));
                                var recentSwaps = new List<Models.Graph.Swap>();
                                int previousCount;

                                do
                                {
                                    // Retrieve the swaps in batches until there are no more fetched, or the configured threshold is reached.
                                    previousCount = recentSwaps.Count;
                                    recentSwaps.AddRange(_mapper.Map<IEnumerable<Models.Graph.Swap>>((await _graphService.SendQuery<SwapsResponse>(
                                        new Dictionary<string, string> {{idName, Graph.Types.Id}},
                                        new Dictionary<string, object>
                                        {
                                            {Graph.OrderBy, JsonHelper.GetJsonPropertyName<SwapType>(nameof(SwapType.Timestamp))},
                                            {Graph.OrderDirection, Graph.Descending},
                                            {Graph.Where, new Dictionary<string, object>
                                            {
                                                {JsonHelper.GetJsonPropertyName<PairResponse>(nameof(PairResponse.Pair)), $"${idName}"},
                                                {$"{JsonHelper.GetJsonPropertyName<SwapType>(pair.GetToken(tokenIn.Id) == pair.Token0 ? nameof(SwapType.Amount0Out) : nameof(SwapType.Amount1Out))}{Graph.Gte}", _settingsOptions.HistoricalAnalysisMinEthOut},
                                                {$"{JsonHelper.GetJsonPropertyName<SwapType>(nameof(SwapType.Timestamp))}{Graph.Lt}", recentSwaps.Any() ? ((DateTimeOffset)recentSwaps.Last().Timestamp).ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds()}
                                            }}
                                        },
                                        new Dictionary<string, HashSet<string>>
                                        {
                                            {nameof(SwapType), new HashSet<string> {nameof(SwapType.Pair)}}
                                        },
                                        new {id = pair.Id},
                                        cancellationToken: cancellationToken)).Swaps));
                                } while (recentSwaps.Count.Between(previousCount, _settingsOptions.HistoricalAnalysisMinFetchThreshold, false));

                                var relevantSwaps = (await Task.WhenAll(recentSwaps.Select(async s => await _ethereumService.GetSwap(s.Transaction.Id)))).Where(s => s != null).ToArray();
                                if (relevantSwaps.Length < _settingsOptions.HistoricalAnalysisMinRelevantSwaps)
                                {
                                    return (false, $"Number of relevant swaps ({relevantSwaps.Length} out of {recentSwaps.Count}) is too low.");
                                }

                                var uniqueRecipients = relevantSwaps.GroupBy(s => s.To).Count();
                                return uniqueRecipients / (double)relevantSwaps.Length < _settingsOptions.MinimumUniqueSellRecipientsRatio
                                    ? (false, $"Number of unique recipients in the relevant swaps ({uniqueRecipients} out of {relevantSwaps.Length}) is too low.")
                                    : (true, default);
                            }
                        };

                        var failures = await DoChecks(swapChecks);
                        if (failures > 0 && _settingsOptions.DiscardSwapAsSoonAsInvalid) return;

                        // No need to perform pair checks on a testnet.
                        if (_batBotOptions.Network == BatBotOptions.Mainnet && !allowed)
                        {
                            var pairFailures = await DoChecks(pairChecks);
                            if (pairFailures > 0)
                            {
                                _pairWhitelist[pair.Id] = false;
                                 if (_settingsOptions.DiscardSwapAsSoonAsInvalid) return;
                            }
                            failures += pairFailures;
                        }

                        if (failures > 0)
                        {
                            await SendPreFrontRunMessage($"🔚 {failures} validation failures found", MessageTier.End);
                            return;
                        }

                        async Task<int> DoChecks(IEnumerable<Func<Task<(bool, string)>>> checks)
                        {
                            var failedChecks = 0;
                            foreach (var check in checks)
                            {
                                var (condition, message) = await check();
                                if (!condition)
                                {
                                    failedChecks++;
                                    await SendPreFrontRunMessage($"⛔ {message}", _settingsOptions.DiscardSwapAsSoonAsInvalid ? MessageTier.End : MessageTier.None);
                                    if (_settingsOptions.DiscardSwapAsSoonAsInvalid) break;
                                }
                            }

                            return failedChecks;
                        }
                    }

                    #endregion

                    #region Preparation

                    var frontRunAmountIn = swap.AmountIn;
                    BigInteger frontRunAmountOutMin;
                    BigInteger targetSwapAmountOut;

                    do
                    {
                        // Find the optimal amount to front run with by inferring the effect of slippage from iterative queries to the smart contract.
                        frontRunAmountOutMin = await GetAmountOut(prepWeb3, frontRunAmountIn, swap.Path);
                        targetSwapAmountOut = await GetAmountOut(prepWeb3, swap.AmountIn + frontRunAmountIn, swap.Path) - frontRunAmountOutMin;

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
                    var slippage = new Rational(frontRunAmountOutMin) / new Rational(targetSwapAmountOut) / (new Rational(frontRunAmountIn) / new Rational(swap.AmountIn)) - 1;
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
                    var frontRunBuyReceipt = await _smartContractService.ContractSendRequestAndWaitForReceipt(frontRunWeb3, _batBotOptions.ContractAddress, frontRunBuyMessage, false);
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

                    var waitTask = Task.Factory.StartNew(async w =>
                    {
                        // Wait for original (target's) swap to be mined.
                        if (!await ((TransactionWaitService.Waiter)w).Wait(swap.Source, cancellationToken) && !_settingsOptions.SellOnFailedTargetSwap)
                        {
                            await _messagingService.SendTxMessage("‼️ Aborting (not selling) due to target swap failing (as configured)", MessageTier.End);
                            return false;
                        }

                        return true;
                    }, waiter, cancellationToken);

                    var approveTask = Task.Run(async () =>
                    {
                        // Ensure that the tokens bought are approved for selling before purchasing.
                        await _messagingService.SendTxMessage($"❓ Checking spender {_batBotOptions.ContractAddress} allowed");
                        var allowed = await _smartContractService.ContractQuery<AllowanceFunction, BigInteger>(frontRunWeb3, frontRunSellMessage.Path.First(), new AllowanceFunction
                        {
                            Owner = _batBotOptions.FrontRunWalletAddress,
                            Spender = _batBotOptions.ContractAddress
                        });

                        if (allowed.IsZero)
                        {
                            await _messagingService.SendTxMessage($"🤝 Approving spender {_batBotOptions.ContractAddress}");
                            var approveReceipt = await _smartContractService.ContractSendRequestAndWaitForReceipt(frontRunWeb3, frontRunSellMessage.Path.First(), new ApproveFunction
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

                    if (!await waitTask.Result || !approveTask.Result) return;

                    #endregion

                    #region Sell

                    frontRunSellMessage.AmountIn = amountBought;
                    await _messagingService.SendTxMessage($"🛒 Selling {Web3.Convert.FromWei(frontRunSellMessage.AmountIn, tokenOut.Decimals)} {tokenOut.Symbol}", MessageTier.Double);
                    TransactionReceipt frontRunSellReceipt;

                    try
                    {
                        // Sell the front run tokens bought earlier.
                        frontRunSellReceipt = await _smartContractService.ContractSendRequestAndWaitForReceipt(frontRunWeb3, _batBotOptions.ContractAddress, frontRunSellMessage);
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
                catch (Exception e) when (e is RpcResponseException || e is RpcClientUnknownException || e is SmartContractRevertException)
                {
                    await _messagingService.SendTxMessage($"❌ Failed with error '{e.Message}'", MessageTier.End);
                }

                #region Local functions

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

                async Task<BigInteger> GetAmountOut(IWeb3 web3, BigInteger amountIn, List<string> path) => (await _smartContractService.ContractQuery<GetAmountsOutFunction, List<BigInteger>>(web3, _batBotOptions.ContractAddress, new GetAmountsOutFunction
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
