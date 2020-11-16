using System.Numerics;
using BatBot.Server.Constants;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace BatBot.Server.Functions
{
    [Function(Uniswap.SwapExactTokensForEth, Uniswap.Types.UInt256Array)]
    public class SwapExactTokensForEthFunction : SwapExactEthForTokensFunction
    {
        [Parameter(Uniswap.Types.UInt256, "amountIn")]
        public BigInteger AmountIn { get; set; }
    }
}
