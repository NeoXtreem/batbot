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
        public virtual List<string> Path { get; set; }

        [Parameter(Uniswap.Types.Address, "to", 3)]
        public virtual string To { get; set; }

        [Parameter(Uniswap.Types.UInt256, "deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
    }
}
