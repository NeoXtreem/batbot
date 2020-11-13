using BatBot.Server.Constants;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace BatBot.Server.Functions
{
    [Function(Uniswap.SwapExactTokensForEthSupportingFeeOnTransferTokens, Uniswap.Types.UInt256Array)]
    public class SwapExactTokensForEthSupportingFeeOnTransferTokensFunction : SwapExactTokensForEthFunction
    {
    }
}
