using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Polly;
using Polly.Bulkhead;
using Polly.CircuitBreaker;

namespace BatBot.Server.Services
{
    public sealed class BackoffService : IDisposable
    {
        private readonly MessagingService _messagingService;
        private readonly AsyncBulkheadPolicy _policyBulkhead = Policy.BulkheadAsync(1, 0);
        private readonly AsyncCircuitBreakerPolicy _policyCircuitBreaker = Policy.Handle<RpcResponseException>().Or<RpcClientUnknownException>().CircuitBreakerAsync(1, TimeSpan.FromSeconds(10));

        public BackoffService(MessagingService messagingService)
        {
            _messagingService = messagingService;
        }

        public async Task Protect(Func<Task> action)
        {
            try
            {
                await _policyCircuitBreaker.ExecuteAsync(action);
            }
            catch (BrokenCircuitException e)
            {
                await _messagingService.SendLogMessage($"Exception: '{e.Message}'.");
            }
            catch (RpcResponseException e)
            {
                await _messagingService.SendLogMessage($"Exception: '{e.Message}'.");
            }
            catch (RpcClientUnknownException e)
            {
                await _messagingService.SendLogMessage($"Exception: '{e.Message}'.");
            }
        }

        public async Task Throttle(Func<Task> action)
        {
            try
            {
                await _policyBulkhead.ExecuteAsync(action);
            }
            catch (BulkheadRejectedException e)
            {
                await _messagingService.SendLogMessage($"Exception: '{e.Message}'.");
            }
        }

        public bool BackOff() => _policyBulkhead.BulkheadAvailableCount == 0;

        public void Dispose() => _policyBulkhead.Dispose();
    }
}
