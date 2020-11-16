using System.Text.Json.Serialization;

namespace BatBot.Server.Dtos.Graph
{
    public class TransactionType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
}
