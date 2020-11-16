using System;
using System.Text.Json.Serialization;

namespace BatBot.Server.Models.Blocknative.Abstractions
{
    public abstract class WebSocketMessage
    {
        protected WebSocketMessage(string categoryCode, string eventCode)
        {
            CategoryCode = categoryCode;
            EventCode = eventCode;
        }

        [JsonPropertyName("timeStamp")]
        public DateTime TimeStamp { get; } = DateTime.UtcNow;

        [JsonPropertyName("dappId")]
        public string DappId { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("categoryCode")]
        public string CategoryCode { get; set; }

        [JsonPropertyName("eventCode")]
        public string EventCode { get; set; }

        [JsonPropertyName("blockchain")]
        public BlockchainJson Blockchain { get; set; }

        public class BlockchainJson
        {
            [JsonPropertyName("system")]
            public string System { get; set; }

            [JsonPropertyName("network")]
            public string Network { get; set; }
        }
    }
}
