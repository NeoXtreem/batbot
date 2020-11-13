using System.Collections.Generic;
using System.Numerics;
using BatBot.Server.Types;

namespace BatBot.Server.Models
{
    public class Swap
    {
        public string TransactionHash { get; set; }

        public BigInteger AmountToSend { get; set; }

        public BigInteger AmountOutMin { get; set; }

        public BigInteger? Gas { get; set; }

        public BigInteger? GasPrice { get; set; }

        public long Deadline { get; set; }

        public List<string> Path { get; set; }

        public TransactionSource Source { get; set; }
    }
}
