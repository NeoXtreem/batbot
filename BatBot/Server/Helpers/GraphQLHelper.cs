using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using BatBot.Server.Attributes;

namespace BatBot.Server.Helpers
{
    internal static class GraphQLHelper
    {
        private static readonly Dictionary<Type, string> Queries = new Dictionary<Type, string>();

        public static string BuildQuery<T>() => BuildQuery(typeof(T), new List<(string, string)>());

        private static string BuildQuery(Type type, ICollection<(string Name, string TypeName)> variableTypes, bool isRoot = true)
        {
            // Cache previous queries in a dictionary for quicker access.
            if (isRoot && Queries.TryGetValue(type, out var query)) return query;

            var properties = type.GetProperties().Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() != null).ToArray();
            if (!properties.Any()) return string.Empty;

            var queryBuilder = new StringBuilder(" {");

            queryBuilder.Append(string.Join(" ", properties.Select(p =>
            {
                var variableName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
                var typeName = p.GetCustomAttribute<GraphQLVariableAttribute>()?.TypeName;

                // The variable type name is stored in the collection passed through the recursive function calls so that it can be used at the root call.
                if (typeName != null)
                {
                    variableTypes.Add((variableName, typeName));
                }

                return new StringBuilder(BuildQuery(p.PropertyType, variableTypes, false))
                    .Insert(0, $"{variableName}{(isRoot ? $"({string.Join(",", variableTypes.Select(x => $"{x.Name}: ${x.Name}"))})" : string.Empty)}")
                    .ToString();
            })));

            if (isRoot)
            {
                queryBuilder.Insert(0, $"query({string.Join(", ", variableTypes.Select(x => $"${x.Name}: {x.TypeName}"))})");
            }

            queryBuilder.Append("}");

            query = queryBuilder.ToString();

            if (isRoot)
            {
                Queries.Add(type, query);
            }

            return query;
        }
    }
}
