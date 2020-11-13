using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using BatBot.Server.Attributes;
using Rationals;

namespace BatBot.Server.Models.Graph
{
    [Description("Pair")]
    public class PairResponse
    {
        [JsonPropertyName("pair")]
        public PairType Pair { get; set; }

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

            [JsonPropertyName("createdAtTimestamp")]
            public string Created { get; set; }

            public DateTime CreatedValue => DateTimeOffset.FromUnixTimeSeconds(long.Parse(Created)).UtcDateTime;

            public Rational GetTokenPrice(string token) => Rational.ParseDecimal(token == Token0.Id ? Token0Price : token == Token1.Id ? Token1Price : throw new InvalidOperationException());
            
            public class TokenType
            {
                [JsonPropertyName("id")]
                public string Id { get; set; }
            }
        }
    }
}
