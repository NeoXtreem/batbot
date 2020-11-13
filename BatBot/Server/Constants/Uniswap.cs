namespace BatBot.Server.Constants
{
    internal static class Uniswap
    {
        public const string SwapExactEthForTokens = "swapExactETHForTokens";
        public const string SwapExactEthForTokensSupportingFeeOnTransferTokens = "swapExactETHForTokensSupportingFeeOnTransferTokens";
        public const string SwapExactTokensForEth = "swapExactTokensForETH";
        public const string SwapExactTokensForEthSupportingFeeOnTransferTokens = "swapExactTokensForETHSupportingFeeOnTransferTokens";

        internal static class Types
        {
            public const string Array = "[]";
            public const string UInt256 = "uint256";
            public const string UInt256Array = UInt256 + Array;
            public const string Address = "address";
            public const string AddressArray = Address + Array;
        }
    }
}
