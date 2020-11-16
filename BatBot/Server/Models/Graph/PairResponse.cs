using System.ComponentModel;
using System.Text.Json.Serialization;
using BatBot.Server.Dtos.Graph;

namespace BatBot.Server.Models.Graph
{
    [Description("Pair")]
    public class PairResponse
    {
        [JsonPropertyName("pair")]
        public PairType Pair { get; set; }
    }
}
