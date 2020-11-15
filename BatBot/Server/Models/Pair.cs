using System;
using Rationals;

namespace BatBot.Server.Models
{
    public class Pair
    {
        public string Id { get; set; }

        public Token Token0 { get; set; }

        public Token Token1 { get; set; }

        public Rational Token0Price { get; set; }

        public Rational Token1Price { get; set; }

        public Rational ReserveUsd { get; set; }

        public DateTime Created { get; set; }

        public Token GetToken(string id) => id == Token0.Id ? Token0 : id == Token1.Id ? Token1 : throw new InvalidOperationException();

        public Rational GetTokenPrice(string id) => GetToken(id) == Token0 ? Token0Price : Token1Price;
    }
}
