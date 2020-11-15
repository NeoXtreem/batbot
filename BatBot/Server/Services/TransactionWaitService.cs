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
        private readonly MessagingService _messagingService;
        private readonly EthereumSubscriptionService _ethereumSubscriptionService;

        private readonly Dictionary<string, EventWaitHandle> _waitHandles = new Dictionary<string, EventWaitHandle>();
        private readonly Dictionary<string, (BigInteger?, TransactionStatus)> _transactionStates = new Dictionary<string, (BigInteger?, TransactionStatus)>();

        public TransactionWaitService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, MessagingService messagingService, EthereumSubscriptionService ethereumSubscriptionService)
        {
            _messagingService = messagingService;
            _ethereumSubscriptionService = ethereumSubscriptionService;
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
        }

        public Waiter GetWaiter(string transactionHash)
        {
            return new Waiter(transactionHash, _waitHandles, _transactionStates, _batBotOptions, _messagingService, _ethereumSubscriptionService);
        }

        public void TransactionReceived(string transactionHash, (BigInteger?, TransactionStatus) transactionState)
        {
            if (!_waitHandles.TryGetValue(transactionHash, out var ewh)) return;
            _transactionStates[transactionHash] = transactionState;
            ewh.Set();
        }

        public sealed class Waiter : IDisposable
        {
            private readonly string _transactionHash;
            private readonly Dictionary<string, EventWaitHandle> _waitHandles;
            private readonly Dictionary<string, (BigInteger?, TransactionStatus)> _transactionStates;
            private readonly BatBotOptions _batBotOptions;
            private readonly MessagingService _messagingService;
            private readonly EthereumSubscriptionService _ethereumSubscriptionService;
            private CancellationTokenSource _cts;

            public Waiter(
                string transactionHash,
                Dictionary<string, EventWaitHandle> waitHandles,
                Dictionary<string, (BigInteger?, TransactionStatus)> transactionStates,
                BatBotOptions batBotOptions,
                MessagingService messagingService,
                EthereumSubscriptionService ethereumSubscriptionService)
            {
                _transactionHash = transactionHash;
                _waitHandles = waitHandles;
                _transactionStates = transactionStates;
                _batBotOptions = batBotOptions;
                _messagingService = messagingService;
                _ethereumSubscriptionService = ethereumSubscriptionService;

                // Create a wait handle to block execution until a state change is detected in the transaction.
                _waitHandles.TryAdd(transactionHash, new EventWaitHandle(false, EventResetMode.ManualReset, _transactionHash));
            }

            public async Task<bool> Wait(TransactionSource transactionSource, CancellationToken cancellationToken)
            {
                await _messagingService.SendTxMessage($"⏳ Waiting for transaction: {_messagingService.GetEtherscanUrl($"tx/{_transactionHash}")}", MessageTier.Double);
                (BigInteger? BlockNumber, TransactionStatus Status) transactionState = (null, TransactionStatus.None);

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

                            // Timer checks periodically for the transaction in case it failed.
                            var timer = new Timer(async s => await CheckTransaction(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

                            await _ethereumSubscriptionService.HandleSubscription(streamingClient, subscription, async () => await subscription.SubscribeAsync(), async l =>
                            {
                                if (l.TransactionHash == _transactionHash)
                                {
                                    await CheckTransaction();
                                }
                            },
                            CheckTransaction, // Check if the transaction has already been mined once the subscription has started, to prevent a race condition.
                            linkedCts.Token);

                            await timer.DisposeAsync();

                            async Task CheckTransaction()
                            {
                                using var client = new WebSocketClient(_batBotOptions.BlockchainEndpointWssUrl);
                                var transactionReceipt = await new EthGetTransactionReceipt(client).SendRequestAsync(_transactionHash);
                                if (!(transactionReceipt?.BlockNumber.Value.IsZero ?? true) && Interlocked.Exchange(ref found, 1) == 0)
                                {
                                    _cts.Cancel();
                                    transactionState = (transactionReceipt.BlockNumber, transactionReceipt.Status.Value == 1 ? TransactionStatus.Confirmed : TransactionStatus.Failed);
                                }
                            }
                        }
                        break;
                    case TransactionSource.BlocknativeWebhook:
                    case TransactionSource.BlocknativeWebSocket:
                        if (transactionSource == TransactionSource.BlocknativeWebhook)
                        {
                            // Watch for the transaction state changing via Blocknative.
                            using var client = new HttpClient { BaseAddress = new Uri(_batBotOptions.BlocknativeApiHttpUrl) };
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_batBotOptions.BlocknativeUsername}:{_batBotOptions.BlocknativePassword}")));
                            var response = await client.PostAsJsonAsync("transaction", new TransactionMessage
                            {
                                ApiKey = _batBotOptions.BlocknativeApiKey,
                                Hash = _transactionHash,
                                Blockchain = _batBotOptions.Blockchain,
                                Network = _batBotOptions.Network
                            }, cancellationToken);

                            // In case Blocknative fails for any reason, fall back to checking the mempool directly.
                            if (!response.IsSuccessStatusCode)
                            {
                                return await Wait(TransactionSource.Mempool, cancellationToken);
                            }
                        }

                        _waitHandles[_transactionHash].WaitOne();
                        _transactionStates.Remove(_transactionHash, out transactionState);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(transactionSource), transactionSource, null);
                }

                string statusIcon;

                switch (transactionState.Status)
                {
                    case TransactionStatus.Cancel:
                        statusIcon = "🚫";
                        break;
                    case TransactionStatus.Confirmed:
                        statusIcon = "✔️";
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
                        throw new InvalidOperationException($"Transaction status '{transactionState.Status}' is not supported.");
                }

                await _messagingService.SendTxMessage($"{statusIcon} Found transaction {_transactionHash}{(transactionState.BlockNumber.HasValue ? $" at {_messagingService.GetEtherscanUrl($"block/{transactionState.BlockNumber}")}" : string.Empty)} (state: {EnumHelper.GetDescriptionFromValue(transactionState.Status)})", MessageTier.Single | MessageTier.End);
                return transactionState.Status == TransactionStatus.Confirmed;
            }

            public void Dispose() => _waitHandles.Remove(_transactionHash);
        }
    }
}
