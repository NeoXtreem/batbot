using System.ComponentModel;

namespace BatBot.Server.Types
{
    public enum TransactionStatus
    {
        None,

        [Description("cancel")]
        Cancel,

        [Description("confirmed")]
        Confirmed,

        [Description("dropped")]
        Dropped,

        [Description("failed")]
        Failed,

        [Description("pending")]
        Pending,

        [Description("speedup")]
        Speedup,

        [Description("stuck")]
        Stuck
    }
}
