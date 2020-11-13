using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Constants;
using Microsoft.AspNetCore.Mvc;
using BatBot.Server.Services;
using BatBot.Server.Types;

namespace BatBot.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BatBotController : ControllerBase
    {
        private readonly MempoolMonitoringService _mempoolMonitoringService;
        private readonly BlocknativeMonitoringService _blocknativeMonitoringService;
        private readonly BlocknativeMessageService _blocknativeMessageService;
        private static CancellationTokenSource _cts;

        public BatBotController(MempoolMonitoringService mempoolMonitoringService, BlocknativeMonitoringService blocknativeMonitoringService, BlocknativeMessageService blocknativeMessageService)
        {
            _mempoolMonitoringService = mempoolMonitoringService;
            _blocknativeMonitoringService = blocknativeMonitoringService;
            _blocknativeMessageService = blocknativeMessageService;
            Reset();
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> MonitorMempool()
        {
            Reset();
            await _mempoolMonitoringService.Subscribe(_cts.Token);
            return Ok();
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> MonitorBlocknativeWebhook()
        {
            Reset();

            if (!(await _blocknativeMonitoringService.RemoveContractFromWebhook()).IsSuccessStatusCode)
            {
                return BadRequest();
            }

            if (!(await _blocknativeMonitoringService.AddContractToWebhook()).IsSuccessStatusCode)
            {
                return BadRequest();
            }

            return Ok();
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> MonitorBlocknativeWebSocket()
        {
            Reset();
            await _blocknativeMonitoringService.Subscribe(_cts.Token);
            return Ok();
        }

        private static void Reset()
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        [HttpPost]
        public async Task Post([FromBody] JsonElement message)
        {
            await _blocknativeMonitoringService.RemoveContractFromWebhook();

            try
            {
                if (message.TryGetProperty(Blocknative.Properties.ContractCall, out var contractCall))
                {
                    await _blocknativeMessageService.Handle(message, contractCall, TransactionSource.BlocknativeWebhook, _cts.Token);
                }
            }
            finally
            {
                await _blocknativeMonitoringService.AddContractToWebhook();
            }
        }
    }
}
