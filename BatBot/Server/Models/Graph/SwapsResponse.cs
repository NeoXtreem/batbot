using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using BatBot.Server.Dtos.Graph;

namespace BatBot.Server.Models.Graph
{
    [Description("Swaps")]
    public class SwapsResponse
    {
        [JsonPropertyName("swaps")]
        public List<SwapType> Swaps { get; set; }
    }
}
