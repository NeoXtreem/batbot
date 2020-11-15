using System.Text.Json.Serialization;
using BatBot.Server.Attributes;

namespace BatBot.Server.Models.Graph.Types
{
    public class SwapType
    {
        [JsonPropertyName("id"), GraphQLVariable("ID")]
        public string Id { get; set; }

        [JsonPropertyName("amount0In")]
        public string Amount0In { get; set; }

        [JsonPropertyName("amount0Out")]
        public string Amount0Out { get; set; }

        [JsonPropertyName("amount1In")]
        public string Amount1In { get; set; }

        [JsonPropertyName("amount1Out")]
        public string Amount1Out { get; set; }
    }
}
