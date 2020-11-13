using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace BatBot.Server.Models.Blocknative
{
    public class AddressMessage
    {
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; }

        [JsonPropertyName("blockchain")]
        public string Blockchain { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("networks")]
        public Collection<string> Networks { get; set; }
    }
}
