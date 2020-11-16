using System.Reflection;
using System.Text.Json.Serialization;

namespace BatBot.Server.Helpers
{
    internal static class JsonHelper
    {
        public static string GetJsonPropertyName<T>(string propertyName) => typeof(T).GetProperty(propertyName)?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
    }
}
