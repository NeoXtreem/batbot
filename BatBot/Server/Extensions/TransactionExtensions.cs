using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace BatBot.Server.Extensions
{
    internal static class TransactionExtensions
    {
        public static bool TryDecodeTransactionToFunctionMessage<T>(this Transaction transaction, out T message)
            where T : FunctionMessage, new()
        {
            if (transaction.IsTransactionForFunctionMessage<T>())
            {
                message = transaction.DecodeTransactionToFunctionMessage<T>();
                return true;
            }

            message = null;
            return false;
        }
    }
}
