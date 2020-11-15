using System.Text.Json.Serialization;
using BatBot.Server.Attributes;

namespace BatBot.Server.Models.Graph.Types
{

    public class PairType
    {
        [JsonPropertyName("id"), GraphQLVariable("ID")]
        public string Id { get; set; }

        [JsonPropertyName("token0")]
        public TokenType Token0 { get; set; }

        [JsonPropertyName("token1")]
        public TokenType Token1 { get; set; }

        [JsonPropertyName("token0Price")]
        public string Token0Price { get; set; }

        [JsonPropertyName("token1Price")]
        public string Token1Price { get; set; }

        [JsonPropertyName("reserveUSD")]
        public string ReserveUsd { get; set; }

        [JsonPropertyName("createdAtTimestamp")]
        public string Created { get; set; }
    }
}
