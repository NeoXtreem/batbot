using System;

namespace BatBot.Server.Types
{
    [Flags]
    public enum MessageTier
    {
        None = 0,
        End = 1,
        Single = 2,
        Double = 4
    }
}
