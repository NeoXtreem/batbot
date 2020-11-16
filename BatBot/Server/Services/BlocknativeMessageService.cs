using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Constants;
using BatBot.Server.Helpers;
using BatBot.Server.Models;
using BatBot.Server.Types;
using Microsoft.Extensions.Logging;

namespace BatBot.Server.Services
{
    public sealed class BlocknativeMessageService
    {
        private readonly ILogger<BlocknativeMessageService> _logger;
        private readonly TransactionProcessorService _transactionProcessorService;
        private readonly TransactionWaitService _transactionWaitService;

        public BlocknativeMessageService(ILogger<BlocknativeMessageService> logger, TransactionProcessorService transactionProcessorService, TransactionWaitService transactionWaitService)
        {
            _logger = logger;
            _transactionProcessorService = transactionProcessorService;
            _transactionWaitService = transactionWaitService;
        }

        public async Task Handle(JsonElement transaction, JsonElement contractCall, TransactionSource transactionSource, CancellationToken cancellationToken)
        {
            if (contractCall.GetProperty(Blocknative.Properties.MethodName).ValueEquals(Uniswap.SwapExactEthForTokens))
            {
                var transactionHash = transaction.GetProperty(Blocknative.Properties.Hash).GetString();
                _logger.LogTrace($"Transaction {transactionHash} received");

                var transactionStatus = EnumHelper.GetValueFromDescription<TransactionStatus>(transaction.GetProperty(Blocknative.Properties.Status).GetString());

                switch (transactionStatus)
                {
                    case TransactionStatus.Pending:
                        var @params = contractCall.GetProperty(Blocknative.Properties.Params);
                        await _transactionProcessorService.Process(new Swap
                        {
                            TransactionHash = transactionHash,
                            AmountIn = TryParseJsonString(transaction, Blocknative.Properties.Value),
                            Gas = transaction.GetProperty(Blocknative.Properties.Gas).GetUInt64(),
                            GasPrice = TryParseJsonString(transaction, Blocknative.Properties.GasPrice),
                            AmountOutMin = TryParseJsonString(@params, Blocknative.Properties.AmountOutMin),
                            Deadline = (long)TryParseJsonString(@params, Blocknative.Properties.Deadline),
                            Path = @params.GetProperty(Blocknative.Properties.Path).EnumerateArray().Select(e => e.GetString()).ToList(),
                            Source = transactionSource
                        }, cancellationToken);

                        static BigInteger TryParseJsonString(JsonElement json, string name) => BigInteger.TryParse(json.GetProperty(name).GetString(), out var value)
                            ? value
                            : throw new ArgumentOutOfRangeException(nameof(json), json, null);
                        break;
                    case TransactionStatus.Cancel:
                    case TransactionStatus.Confirmed:
                    case TransactionStatus.Dropped:
                    case TransactionStatus.Failed:
                    case TransactionStatus.Stuck:
                        var blockNumber = transaction.GetProperty(Blocknative.Properties.BlockNumber);
                        _transactionWaitService.TransactionReceived(transactionHash, (blockNumber.ValueKind == JsonValueKind.Number ? blockNumber.GetInt64() : (BigInteger?)null, transactionStatus));
                        break;
                    case TransactionStatus.Speedup:
                        // Ignore.
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(transaction), transaction, null);
                }
            }
        }
    }
}
