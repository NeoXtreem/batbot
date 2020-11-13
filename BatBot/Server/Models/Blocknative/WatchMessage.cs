using System.Text.Json.Serialization;

namespace BatBot.Server.Models.Blocknative
{
    public class WatchMessage : WebSocketMessage
    {
        public WatchMessage() : base(Constants.Blocknative.CategoryCodes.AccountAddress, Constants.Blocknative.EventCodes.Watch)
        {
        }

        [JsonPropertyName("account")]
        public AccountJson Account { get; set; }

        public class AccountJson
        {
            [JsonPropertyName("address")]
            public string Address { get; set; }
        }
    }
}
