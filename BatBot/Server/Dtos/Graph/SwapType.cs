using System.Text.Json.Serialization;

namespace BatBot.Server.Dtos.Graph
{
    public class SwapType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("pair")]
        public PairType Pair { get; set; }

        [JsonPropertyName("transaction")]
        public TransactionType Transaction { get; set; }

        [JsonPropertyName("amount0Out")]
        public string Amount0Out { get; set; }

        [JsonPropertyName("amount1Out")]
        public string Amount1Out { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }
}
