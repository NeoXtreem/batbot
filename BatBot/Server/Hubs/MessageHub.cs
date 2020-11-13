using System.Threading.Tasks;
using BatBot.Shared;
using Microsoft.AspNetCore.SignalR;

namespace BatBot.Server.Hubs
{
    public class MessageHub : Hub
    {
        public async Task SendTxMessage(Message message)
        {
            await Clients.All.SendAsync("ReceiveTxMessage", message);
        }

        public async Task SendLogMessage(Message message)
        {
            await Clients.All.SendAsync("ReceiveLogMessage", message);
        }
    }
}
