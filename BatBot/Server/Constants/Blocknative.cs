namespace BatBot.Server.Constants
{
    internal static class Blocknative
    {
        internal static class Properties
        {
            public const string Status = "status";
            public const string Event = "event";
            public const string CategoryCode = "categoryCode";
            public const string ContractCall = "contractCall";
            public const string Transaction = "transaction";
            public const string Timestamp = "timeStamp";

            // Contract call properties
            public const string MethodName = "methodName";
            public const string Params = "params";
            public const string AmountOutMin = "amountOutMin";
            public const string Deadline = "deadline";
            public const string Path = "path";

            // Transaction properties
            public const string Hash = "hash";
            public const string BlockNumber = "blockNumber";
            public const string Value = "value";
            public const string Gas = "gas";
            public const string GasPrice = "gasPrice";
        }

        internal static class Statuses
        {
            public const string Ok = "ok";
        }

        internal static class CategoryCodes
        {
            public const string Initialize = "initialize";
            public const string Configs = "configs";
            public const string AccountAddress = "accountAddress";
            public const string ActiveAddress = "activeAddress";
        }

        internal static class EventCodes
        {
            public const string CheckDappId = "checkDappId";
            public const string Put = "put";
            public const string Watch = "watch";
        }

        internal static class Filters
        {
            public const string Gte = "gte";
            public const string Lte = "lte";
        }
    }
}
