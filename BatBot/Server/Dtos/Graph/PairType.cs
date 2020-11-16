using System.Text.Json.Serialization;

namespace BatBot.Server.Dtos.Graph
{
    public class PairType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("token0")]
        public TokenType Token0 { get; set; }

        [JsonPropertyName("token1")]
        public TokenType Token1 { get; set; }

        [JsonPropertyName("reserveUSD")]
        public string ReserveUsd { get; set; }

        [JsonPropertyName("createdAtTimestamp")]
        public string Created { get; set; }
    }
}
