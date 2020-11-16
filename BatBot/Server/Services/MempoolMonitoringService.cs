using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Models;
using Microsoft.Extensions.Options;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

namespace BatBot.Server.Services
{
    public sealed class MempoolMonitoringService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly MessagingService _messagingService;
        private readonly BackoffService _backoffService;
        private readonly TransactionProcessorService _transactionProcessorService;
        private readonly EthereumService _ethereumService;

        public MempoolMonitoringService(
            IOptionsFactory<BatBotOptions> batBotOptionsFactory,
            MessagingService messagingService,
            BackoffService backoffService,
            TransactionProcessorService transactionProcessorService,
            EthereumService ethereumService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _messagingService = messagingService;
            _backoffService = backoffService;
            _transactionProcessorService = transactionProcessorService;
            _ethereumService = ethereumService;
        }

        public async Task Subscribe(CancellationToken cancellationToken)
        {
            // http://docs.nethereum.com/en/latest/nethereum-subscriptions-streaming/
            using var streamingClient = new StreamingWebSocketClient(_batBotOptions.BlockchainEndpointWssUrl);
            var subscription = new EthNewPendingTransactionObservableSubscription(streamingClient);

            await _ethereumService.HandleSubscription(streamingClient, subscription, async () => await subscription.SubscribeAsync(), async th =>
            {
                await _messagingService.SendLogMessage($"Pending Transaction Hash: {th}");

                // Don't process transactions (discard) when one is being processed for front running.
                if (_backoffService.BackOff()) return;

                await _backoffService.Protect(async () => await _transactionProcessorService.Process(await _ethereumService.GetSwap(th), cancellationToken));
            }, null, cancellationToken);
        }
    }
}
