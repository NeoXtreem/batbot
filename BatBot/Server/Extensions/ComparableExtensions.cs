using System;

namespace BatBot.Server.Extensions
{
    internal static class ComparableExtensions
    {
        public static bool Between<T>(this T value, T min, T max, bool inclusive = true) where T : IComparable<T>
        {
            return inclusive ? value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0 : value.CompareTo(min) > 0 && value.CompareTo(max) < 0;
        }
    }
}
