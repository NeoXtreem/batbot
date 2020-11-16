using System.Collections.Generic;
using System.Numerics;
using BatBot.Server.Constants;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace BatBot.Server.Functions.Abstractions
{
    public abstract class SwapFunction : FunctionMessage
    {
        [Parameter(Uniswap.Types.AddressArray, "path", 2)]
        public List<string> Path { get; set; }

        [Parameter(Uniswap.Types.Address, "to", 3)]
        public string To { get; set; }

        [Parameter(Uniswap.Types.UInt256, "deadline", 4)]
        public BigInteger Deadline { get; set; }
    }
}
