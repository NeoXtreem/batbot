using System;
using System.ComponentModel;
using System.Linq;

namespace BatBot.Server.Helpers
{
    internal static class EnumHelper
    {
        public static string GetDescriptionFromValue<T>(T value) where T : Enum
        {
            var name = Enum.GetName(typeof(T), value);

            if (name != null)
            {
                var field = value.GetType().GetField(name);
                if (field != null)
                {
                    return ((DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)))?.Description;
                }
            }

            return null;
        }

        public static T GetValueFromDescription<T>(string description) where T : Enum
        {
            return (T)typeof(T).GetFields().Single(f =>
                Attribute.GetCustomAttribute(f, typeof(DescriptionAttribute)) is DescriptionAttribute attribute && attribute.Description == description ||
                f.Name == description).GetValue(null);
        }
    }
}
