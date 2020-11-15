using System.ComponentModel;
using System.Text.Json.Serialization;
using BatBot.Server.Models.Graph.Types;

namespace BatBot.Server.Models.Graph
{
    [Description("Swap")]
    public class SwapResponse
    {
        [JsonPropertyName("token")]
        public SwapType Swap { get; set; }
    }
}
