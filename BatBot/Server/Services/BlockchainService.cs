using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BatBot.Server.Models;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Rationals;

namespace BatBot.Server.Services
{
    public class BlockchainService
    {
        private readonly SettingsOptions _settingsOptions;
        private readonly MessagingService _messagingService;

        public BlockchainService(IOptionsFactory<SettingsOptions> settingsOptionsFactory, MessagingService messagingService)
        {
            _settingsOptions = settingsOptionsFactory.Create(Options.DefaultName);
            _messagingService = messagingService;
        }

        public async Task<T2> ContractQuery<T1, T2>(IWeb3 web3, string contractAddress, T1 functionMessage = null) where T1 : FunctionMessage, new()
        {
            var result = await web3.Eth.GetContractQueryHandler<T1>().QueryAsync<T2>(contractAddress, functionMessage);
            await _messagingService.SendLogMessage($"⚡ Executed '{typeof(T1).GetCustomAttribute<FunctionAttribute>()?.Name}' at {_messagingService.GetEtherscanUrl($"contract/{contractAddress}")} with result {(result is IEnumerable enumerable && !(result is string) ? string.Join(",", enumerable.Cast<object>()) : result.ToString())}");
            return result;
        }

        public async Task<TransactionReceipt> ContractSendRequestAndWaitForReceipt<T>(IWeb3 web3, string contractAddress, T functionMessage, bool escalateGas = true) where T : FunctionMessage, new()
        {
            var handler = web3.Eth.GetContractTransactionHandler<T>();

            if (escalateGas)
            {
                await EstimateGas(web3, contractAddress, functionMessage);
                IncreaseGas(); // Give the gas a boost upfront as it tends to fail with the estimated gas.

                for (var i = 0; i < _settingsOptions.GasEscalationRetries; i++)
                {
                    try
                    {
                        var transactionReceipt = await SendRequestAndWaitForReceiptAsync();
                        if (transactionReceipt.Failed())
                        {
                            await _messagingService.SendTxMessage("❌ Failed - increasing gas limit and trying again");
                            IncreaseGas();
                            continue;
                        }

                        return transactionReceipt;
                    }
                    catch (RpcResponseException e)
                    {
                        if (e.Message.Contains("gas allowance"))
                        {
                            await _messagingService.SendTxMessage($"❌ Failed with error '{e.Message}' - increasing gas limit and trying again");
                            IncreaseGas();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                void IncreaseGas() => functionMessage.Gas = (functionMessage.Gas.GetValueOrDefault() * (Rational)_settingsOptions.GasEscalationFactor).WholePart;
            }

            return await SendRequestAndWaitForReceiptAsync();

            async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync()
            {
                await _messagingService.SendTxMessage($"⚡ Executing '{typeof(T).GetCustomAttribute<FunctionAttribute>()?.Name}' at {_messagingService.GetEtherscanUrl($"contract/{contractAddress}")} with gas limit {(functionMessage.Gas.HasValue ? functionMessage.Gas.ToString() : "unset")}");
                return await handler.SendRequestAndWaitForReceiptAsync(contractAddress, functionMessage);
            }
        }

        async Task EstimateGas<T>(IWeb3 web3, string contractAddress, T functionMessage) where T : FunctionMessage, new()
        {
            try
            {
                //TODO: Convert to ??= operator when this issue is fixed: https://github.com/dotnet/roslyn/issues/49148
                if (functionMessage.Gas is null)
                {
                    functionMessage.Gas = await web3.Eth.GetContractTransactionHandler<T>().EstimateGasAsync(contractAddress, functionMessage);
                }
            }
            catch (SmartContractRevertException e)
            {
                await _messagingService.SendTxMessage($"❌ Gas estimation for '{typeof(T).GetCustomAttribute<FunctionAttribute>()?.Name}' failed with {e.Message}");
                throw;
            }
        }
    }
}
