using BatBot.Server.Models.Blocknative.Abstractions;

namespace BatBot.Server.Models.Blocknative
{
    public class InitializeMessage : WebSocketMessage
    {
        public InitializeMessage() : base(Constants.Blocknative.CategoryCodes.Initialize, Constants.Blocknative.EventCodes.CheckDappId)
        {
        }
    }
}
