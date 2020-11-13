using System.Text.Json.Serialization;

namespace BatBot.Server.Models.Blocknative
{
    public class TransactionMessage
    {
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; }

        [JsonPropertyName("blockchain")]
        public string Blockchain { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        [JsonPropertyName("network")]
        public string Network { get; set; }
    }
}
