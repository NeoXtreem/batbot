using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Reactive.RpcStreaming;

namespace BatBot.Server.Services
{
    public sealed class EthereumSubscriptionService
    {
        private readonly MessagingService _messagingService;

        public EthereumSubscriptionService(MessagingService messagingService)
        {
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
    }
}
