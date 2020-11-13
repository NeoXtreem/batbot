using BatBot.Server.Constants;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace BatBot.Server.Functions
{
    [Function(Uniswap.SwapExactEthForTokensSupportingFeeOnTransferTokens, Uniswap.Types.UInt256Array)]
    public class SwapExactEthForTokensSupportingFeeOnTransferTokensFunction : SwapExactEthForTokensFunction
    {
    }
}
