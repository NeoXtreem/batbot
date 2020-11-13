using System;

namespace BatBot.Server.Extensions
{
    internal static class ComparableExtensions
    {
        public static bool Between<T>(this T value, T min, T max) where T : IComparable<T> => value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    }
}
