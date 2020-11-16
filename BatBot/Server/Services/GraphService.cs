using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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

        public GraphService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, MessagingService messagingService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _messagingService = messagingService;
        }

        public async Task<T> SendQuery<T>(Dictionary<string, string> variableTypes = null, Dictionary<string, object> filters = null, Dictionary<string, HashSet<string>> exclusions = null, object variables = null, string operationName = null, CancellationToken cancellationToken = default)
        {
            using var graphQLClient = new GraphQLHttpClient(_batBotOptions.UniswapSubgraphUrl, new SystemTextJsonSerializer());
            await _messagingService.SendLogMessage($"⚡ Sending Graph query '{typeof(T).GetCustomAttribute<DescriptionAttribute>()?.Description}' with variables '{variables}'");
            return (await graphQLClient.SendQueryAsync<T>(new GraphQLRequest(BuildQuery(typeof(T), variableTypes, filters, exclusions), variables, operationName), cancellationToken)).Data;
        }

        private static string BuildQuery(Type type, Dictionary<string, string> variableTypes = null, Dictionary<string, object> filters = null, IReadOnlyDictionary<string, HashSet<string>> exclusions = null)
        {
            var properties = type.GetProperties().Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() != null).ToArray();
            if (!properties.Any()) return string.Empty;

            var queryBuilder = new StringBuilder(" {");

            // The Where clause ensures excluded properties are not part of the query.
            queryBuilder.Append(string.Join(" ", properties.Where(p => exclusions is null || !exclusions.TryGetValue(type.Name, out var excluded) || !excluded.Contains(p.Name)).Select(p =>
            {
                return new StringBuilder(BuildQuery(p.PropertyType.IsConstructedGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ? p.PropertyType.GetGenericArguments().Single() : p.PropertyType, exclusions: exclusions))
                    .Insert(0, $"{p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name}{(filters?.Any() == true ? $"({BuildFilter(filters)})" : string.Empty)}")
                    .ToString();

                static string BuildFilter(Dictionary<string, object> filters)
                {
                    return string.Join(", ", filters.Select(f =>
                    {
                        var (key, value) = f;
                        return $"{key}: {(value is Dictionary<string, object> @object ? $"{{{BuildFilter(@object)}}}" : $"{value}")}";
                    }));
                }
            })));

            if (variableTypes?.Any() == true)
            {
                queryBuilder.Insert(0, $"query({string.Join(", ", variableTypes.Select(x => $"${x.Key}: {x.Value}"))})");
            }

            return queryBuilder.Append("}").ToString();
        }
    }
}
