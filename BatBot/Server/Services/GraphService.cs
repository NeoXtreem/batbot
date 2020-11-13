using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Attributes;
using BatBot.Server.Models;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Options;

namespace BatBot.Server.Services
{
    public class GraphService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly MessagingService _messagingService;

        private readonly Dictionary<Type, string> _queries = new Dictionary<Type, string>();

        public GraphService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, MessagingService messagingService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _messagingService = messagingService;
        }

        public async Task<T> SendQuery<T>(object variables = null, string operationName = null, CancellationToken cancellationToken = default)
        {
            using var graphQLClient = new GraphQLHttpClient(_batBotOptions.UniswapSubgraphUrl, new SystemTextJsonSerializer());

            await _messagingService.SendLogMessage($"⚡ Sending Graph query '{typeof(T).GetCustomAttribute<DescriptionAttribute>()?.Description}' with variables '{variables}'");
            return (await graphQLClient.SendQueryAsync<T>(new GraphQLRequest(BuildQuery<T>(), variables, operationName), cancellationToken)).Data;
        }

        public string BuildQuery<T>() => BuildQuery(typeof(T), new List<(string, string)>());

        private string BuildQuery(Type type, ICollection<(string Name, string TypeName)> variableTypes, bool isRoot = true)
        {
            // Cache previous queries in a dictionary for quicker access.
            if (isRoot && _queries.TryGetValue(type, out var query)) return query;

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
                _queries.Add(type, query);
            }

            return query;
        }
    }
}
