using System.Text.Json.Serialization;

namespace BatBot.Server.Dtos.Graph
{
    public class TokenType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("decimals")]
        public string Decimals { get; set; }

        [JsonPropertyName("symbol")]
        public string Symbol { get; set; }

        [JsonPropertyName("totalSupply")]
        public string TotalSupply { get; set; }
    }
}
