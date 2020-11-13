using System.ComponentModel;
using System.Text.Json.Serialization;
using BatBot.Server.Attributes;

namespace BatBot.Server.Models.Graph
{
    [Description("Token")]
    public class TokenResponse
    {
        [JsonPropertyName("token")]
        public TokenType Token { get; set; }

        public class TokenType
        {
            [JsonPropertyName("id"), GraphQLVariable("ID")]
            public string Id { get; set; }

            [JsonPropertyName("decimals")]
            public string Decimals { get; set; }

            [JsonPropertyName("symbol")]
            public string Symbol { get; set; }

            [JsonPropertyName("totalSupply")]
            public string TotalSupply { get; set; }
        }
    }
}
