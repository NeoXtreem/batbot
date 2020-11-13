using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace BatBot.Server.Functions
{
    [Function("factory", "address")]
    public class FactoryFunction : FunctionMessage
    {
    }
}
