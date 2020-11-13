using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace BatBot.Server.Functions
{
    [Function("getPair", "address")]
    public class GetPairFunction : FunctionMessage
    {
        [Parameter("address", "tokenA", 3)]
        public virtual string TokenA { get; set; }

        [Parameter("address", "tokenB", 3)]
        public virtual string TokenB { get; set; }
    }
}
