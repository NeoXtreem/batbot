using System;
using Rationals;

namespace BatBot.Server.Models.Graph
{
    public class Pair
    {
        public string Id { get; set; }

        public Token Token0 { get; set; }

        public Token Token1 { get; set; }

        public Rational ReserveUsd { get; set; }

        public DateTime Created { get; set; }

        public Token GetToken(string id) => id.Equals(Token0.Id, StringComparison.OrdinalIgnoreCase)
            ? Token0
            : id.Equals(Token1.Id, StringComparison.OrdinalIgnoreCase)
                ? Token1
                : throw new ArgumentOutOfRangeException(nameof(id), id, "Token ID does not match either token in the pair.");
    }
}
