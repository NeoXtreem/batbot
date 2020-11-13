using System.Numerics;

namespace BatBot.Server.Models
{
    public class Token
    {
        public string Address { get; set; }

        public int Decimals { get; set; }

        public string Symbol { get; set; }

        public BigInteger TotalSupply { get; set; }
    }
}
