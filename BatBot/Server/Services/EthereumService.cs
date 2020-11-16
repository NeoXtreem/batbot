using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using BatBot.Server.Extensions;
using BatBot.Server.Functions;
using BatBot.Server.Models;
using BatBot.Server.Types;
using Microsoft.Extensions.Options;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Reactive.RpcStreaming;

namespace BatBot.Server.Services
{
    public sealed class EthereumService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly IMapper _mapper;
        private readonly MessagingService _messagingService;

        public EthereumService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, IMapper mapper, MessagingService messagingService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _mapper = mapper;
            _messagingService = messagingService;
        }

        public async Task HandleSubscription<T>(
            IStreamingClient client,
            RpcStreamingSubscriptionObservableHandler<T> subscription,
            Func<Task> subscribeFunc,
            Action<T> onNext,
            Func<Task> subscribeResponseFunc,
            CancellationToken cancellationToken)
        {
            subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(onNext);
            var subscribed = false;

            subscription.GetSubscribeResponseAsObservable().Subscribe(async id =>
            {
                subscribed = true;
                subscribeResponseFunc?.Invoke();
                await _messagingService.SendLogMessage($"Subscribed ID: {id}");
            });

            subscription.GetUnsubscribeResponseAsObservable().Subscribe(async r =>
            {
                subscribed = false;
                await _messagingService.SendLogMessage($"Unsubscribed result: {r}");
            });

            await client.StartAsync();
            await subscribeFunc();
            SpinWait.SpinUntil(() => subscribed && cancellationToken.IsCancellationRequested);
            await subscription.UnsubscribeAsync();
            SpinWait.SpinUntil(() => !subscribed);
        }

        public async Task<Swap> GetSwap(string transactionHash)
        {
            using var client = new WebSocketClient(_batBotOptions.BlockchainEndpointWssUrl);
            var transaction = await new EthGetTransactionByHash(client).SendRequestAsync(transactionHash);

            if (transaction?.To == _batBotOptions.ContractAddress)
            {
                if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactEthForTokensFunction ethForTokensMessage))
                {
                    return Map(ethForTokensMessage);
                }

                if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactEthForTokensSupportingFeeOnTransferTokensFunction ethForTokensFeeMessage))
                {
                    return Map(ethForTokensFeeMessage);
                }

                if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactTokensForEthFunction tokensForEthMessage))
                {
                    return Map(tokensForEthMessage);
                }

                if (transaction.TryDecodeTransactionToFunctionMessage(out SwapExactEthForTokensSupportingFeeOnTransferTokensFunction tokensForEthFeeMessage))
                {
                    return Map(tokensForEthFeeMessage);
                }

                Swap Map(SwapExactEthForTokensFunction message)
                {
                    var swap = _mapper.Map<Swap>(message);
                    swap.TransactionHash = transactionHash;
                    swap.Source = TransactionSource.Mempool;
                    return swap;
                }
            }

            return null;
        }
    }
}
