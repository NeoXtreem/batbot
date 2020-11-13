﻿using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace BatBot.Server.Dtos
{
    [Event("Swap")]
    internal class SwapEventDTO : IEventDTO
    {
        [Parameter("address", "sender", 1, true)]
        public virtual string Sender { get; set; }

        [Parameter("uint256", "amount0In", 2, false)]
        public virtual BigInteger Amount0In { get; set; }

        [Parameter("uint256", "amount1In", 3, false)]
        public virtual BigInteger Amount1In { get; set; }

        [Parameter("uint256", "amount0Out", 4, false)]
        public virtual BigInteger Amount0Out { get; set; }

        [Parameter("uint256", "amount1Out", 5, false)]
        public virtual BigInteger Amount1Out { get; set; }

        [Parameter("address", "to", 6, true)]
        public virtual string To { get; set; }
    }
}
