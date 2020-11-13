using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using BatBot.Server.Extensions;
using BatBot.Server.Functions;
using BatBot.Server.Models;
using BatBot.Server.Types;
using Microsoft.Extensions.Options;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

namespace BatBot.Server.Services
{
    public sealed class MempoolMonitoringService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly IMapper _mapper;
        private readonly MessagingService _messagingService;
        private readonly BackoffService _backoffService;
        private readonly TransactionProcessorService _transactionProcessorService;
        private readonly EthereumSubscriptionService _ethereumSubscriptionService;

        public MempoolMonitoringService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, IMapper mapper, MessagingService messagingService, BackoffService backoffService, TransactionProcessorService transactionProcessorService, EthereumSubscriptionService ethereumSubscriptionService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _mapper = mapper;
            _messagingService = messagingService;
            _backoffService = backoffService;
            _transactionProcessorService = transactionProcessorService;
            _ethereumSubscriptionService = ethereumSubscriptionService;
        }

        public async Task Subscribe(CancellationToken cancellationToken)
        {
            // http://docs.nethereum.com/en/latest/nethereum-subscriptions-streaming/
            using var streamingClient = new StreamingWebSocketClient(_batBotOptions.BlockchainEndpointWssUrl);
            var subscription = new EthNewPendingTransactionObservableSubscription(streamingClient);

            await _ethereumSubscriptionService.HandleSubscription(streamingClient, subscription, async () => await subscription.SubscribeAsync(), async th =>
            {
                await _messagingService.SendLogMessage($"Pending Transaction Hash: {th}");

                // Don't process transactions (discard) when one is being processed for front running.
                if (_backoffService.BackOff()) return;

                await _backoffService.Protect(async () =>
                {
                    using var client = new WebSocketClient(_batBotOptions.BlockchainEndpointWssUrl);
                    var transaction = await new EthGetTransactionByHash(client).SendRequestAsync(th);

                    if (transaction?.To == _batBotOptions.ContractAddress)
                    {
                        if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactTokensForEthFunction tokensForEthMessage))
                        {
                            //await HandleMessage(tokensForEthMessage);
                        }
                        else if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactTokensForEthSupportingFeeOnTransferTokensFunction tokensForEthFeeMessage))
                        {
                            //await HandleMessage(tokensForEthFeeMessage);
                        }
                        else if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactEthForTokensFunction ethForTokensMessage))
                        {
                            var swap = _mapper.Map<Swap>(ethForTokensMessage);
                            swap.TransactionHash = th;
                            swap.Source = TransactionSource.Mempool;
                            await _transactionProcessorService.Process(swap, cancellationToken);
                        }
                        else if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactEthForTokensSupportingFeeOnTransferTokensFunction ethForTokensFeeMessage))
                        {
                            //var swap = _mapper.Map<Swap>(ethForTokensFeeMessage);
                            //swap.TransactionHash = th;
                            //swap.Source = TransactionSource.Mempool;
                            //await _transactionProcessorService.Process(swap, cancellationToken);
                        }
                    }
                });
            }, null, cancellationToken);
        }
    }
}
