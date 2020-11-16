using System.Numerics;
using BatBot.Server.Constants;
using BatBot.Server.Functions.Abstractions;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace BatBot.Server.Functions
{
    [Function(Uniswap.SwapExactEthForTokens, Uniswap.Types.UInt256Array)]
    public class SwapExactEthForTokensFunction : SwapFunction
    {
        [Parameter(Uniswap.Types.UInt256, "amountOutMin", 1)]
        public virtual BigInteger AmountOutMin { get; set; }
    }
}
