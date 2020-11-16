using JetBrains.Annotations;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace BatBot.Server.Functions
{
    [Function("getPair", "address")]
    public class GetPairFunction : FunctionMessage
    {
        [Parameter("address", "tokenA")]
        public string TokenA { [UsedImplicitly] get; set; }

        [Parameter("address", "tokenB", 2)]
        public string TokenB { [UsedImplicitly] get; set; }
    }
}
