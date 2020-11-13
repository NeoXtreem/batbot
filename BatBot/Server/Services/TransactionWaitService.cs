using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Helpers;
using BatBot.Server.Models;
using BatBot.Server.Models.Blocknative;
using BatBot.Server.Types;
using Microsoft.Extensions.Options;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

namespace BatBot.Server.Services
{
    public sealed class TransactionWaitService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly MessagingService _messagingServiceService;
        private readonly EthereumSubscriptionService _ethereumSubscriptionService;
        private CancellationTokenSource _cts;

        private readonly Dictionary<string, EventWaitHandle> _waitHandles = new Dictionary<string, EventWaitHandle>();
        private readonly Dictionary<string, bool> _transactionStates = new Dictionary<string, bool>();

        public TransactionWaitService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, MessagingService messagingService, EthereumSubscriptionService ethereumSubscriptionService)
        {
            _messagingServiceService = messagingService;
            _ethereumSubscriptionService = ethereumSubscriptionService;
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
        }

        public void AddTransaction(string transactionHash)
        {
            // Create a wait handle to block execution until a state change is detected in the transaction.
            _waitHandles.Add(transactionHash, new EventWaitHandle(false, EventResetMode.ManualReset, transactionHash));
        }

        public async Task<bool> WaitForTransaction(string transactionHash, TransactionSource transactionSource, CancellationToken cancellationToken)
        {
            await _messagingServiceService.SendTxMessage($"⏳ Waiting for transaction: {_messagingServiceService.GetEtherscanUrl($"tx/{transactionHash}")}", MessageTier.Double);

            switch (transactionSource)
            {
                case TransactionSource.Mempool:
                {
                    using var streamingClient = new StreamingWebSocketClient(_batBotOptions.BlockchainEndpointWssUrl);
                    var subscription = new EthLogsObservableSubscription(streamingClient);

                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
                    var found = 0;
                    var state = false;

                    // Timer checks periodically for the transaction in case it failed.
                    var timer = new Timer(async s => await CheckTransaction(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

                    await _ethereumSubscriptionService.HandleSubscription(streamingClient, subscription, async () => await subscription.SubscribeAsync(), async l =>
                    {
                        if (l.TransactionHash == transactionHash)
                        {
                            await CheckTransaction();
                        }
                    },
                    CheckTransaction, // Check if the transaction has already been mined once the subscription has started, to prevent a race condition.
                    linkedCts.Token);

                    await timer.DisposeAsync();
                    return state;

                    async Task CheckTransaction()
                    {
                        using var client = new WebSocketClient(_batBotOptions.BlockchainEndpointWssUrl);
                        var transactionReceipt = await new EthGetTransactionReceipt(client).SendRequestAsync(transactionHash);
                        if (!(transactionReceipt?.BlockNumber.Value.IsZero ?? true) && Interlocked.Exchange(ref found, 1) == 0)
                        {
                            _cts.Cancel();
                            state = transactionReceipt.Status.Value == 1;
                            await _messagingServiceService.SendTxMessage($"{(state ? "✔️" : "❌")} Found transaction at {_messagingServiceService.GetEtherscanUrl($"block/{transactionReceipt.BlockNumber}")}", MessageTier.End | MessageTier.Single);
                        }
                    }
                }
                case TransactionSource.BlocknativeWebhook:
                case TransactionSource.BlocknativeWebSocket:
                {
                    if (transactionSource == TransactionSource.BlocknativeWebhook)
                    {
                        // Watch for the transaction state changing via Blocknative.
                        using var client = new HttpClient {BaseAddress = new Uri(_batBotOptions.BlocknativeApiHttpUrl)};
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_batBotOptions.BlocknativeUsername}:{_batBotOptions.BlocknativePassword}")));
                        var response = await client.PostAsJsonAsync("transaction", new TransactionMessage
                        {
                            ApiKey = _batBotOptions.BlocknativeApiKey,
                            Hash = transactionHash,
                            Blockchain = _batBotOptions.Blockchain,
                            Network = _batBotOptions.Network
                        }, cancellationToken);

                        // In case Blocknative fails for any reason, fall back to checking the mempool directly.
                        if (!response.IsSuccessStatusCode)
                        {
                            return await WaitForTransaction(transactionHash, TransactionSource.Mempool, cancellationToken);
                        }
                    }

                    if (_waitHandles.TryGetValue(transactionHash, out var ewh))
                    {
                        ewh.WaitOne();
                    }

                    return _transactionStates.Remove(transactionHash, out var state) && state;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(transactionSource), transactionSource, null);
            }
        }

        public async Task TransactionReceived(string transactionHash, BigInteger? blockNumber, TransactionStatus transactionStatus)
        {
            if (_waitHandles.Remove(transactionHash, out var ewh))
            {
                string statusIcon;
                var state = false;

                switch (transactionStatus)
                {
                    case TransactionStatus.Cancel:
                        statusIcon = "🚫";
                        break;
                    case TransactionStatus.Confirmed:
                        statusIcon = "✔️";
                        state = true;
                        break;
                    case TransactionStatus.Dropped:
                        statusIcon = "🛑";
                        break;
                    case TransactionStatus.Failed:
                        statusIcon = "❌";
                        break;
                    case TransactionStatus.Stuck:
                        statusIcon = "⚠️";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(transactionStatus), transactionStatus, null);
                }

                if (_transactionStates.TryAdd(transactionHash, state))
                {
                    await _messagingServiceService.SendTxMessage($"{statusIcon} Found transaction {transactionHash}{(blockNumber.HasValue ? $" at {_messagingServiceService.GetEtherscanUrl($"block/{blockNumber}")}" : string.Empty)} (state: {EnumHelper.GetDescriptionFromValue(transactionStatus)})");
                }

                ewh.Set();
            }
        }
    }
}
