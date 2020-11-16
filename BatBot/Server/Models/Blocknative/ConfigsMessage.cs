using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using BatBot.Server.Models.Blocknative.Abstractions;

namespace BatBot.Server.Models.Blocknative
{
    public class ConfigsMessage : WebSocketMessage
    {
        public ConfigsMessage() : base(Constants.Blocknative.CategoryCodes.Configs, Constants.Blocknative.EventCodes.Put)
        {
        }

        [JsonPropertyName("config")]
        public ConfigJson Config { get; set; }

        public class ConfigJson
        {
            [JsonPropertyName("filters")]
            public Collection<Dictionary<string, object>> Filters { get; set; }

            [JsonPropertyName("scope")]
            public string Scope { get; set; }
        }
    }
}
