﻿using System.ComponentModel;
using System.Text.Json.Serialization;
using BatBot.Server.Models.Graph.Types;

namespace BatBot.Server.Models.Graph
{
    [Description("Pair")]
    public class PairResponse
    {
        [JsonPropertyName("pair")]
        public PairType Pair { get; set; }
    }
}
