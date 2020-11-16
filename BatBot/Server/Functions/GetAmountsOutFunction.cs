using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace BatBot.Server.Functions
{
    [Function("getAmountsOut", "uint256[]")]
    public class GetAmountsOutFunction : FunctionMessage
    {
        [Parameter("uint256", "amountIn")]
        public BigInteger AmountIn { get; set; }

        [Parameter("address[]", "path", 2)]
        public List<string> Path { get; set; }
    }
}
