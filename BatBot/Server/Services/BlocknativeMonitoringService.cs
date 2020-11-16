using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Constants;
using BatBot.Server.Converters;
using BatBot.Server.Models;
using BatBot.Server.Models.Blocknative;
using BatBot.Server.Models.Blocknative.Abstractions;
using BatBot.Server.Types;
using Microsoft.Extensions.Options;
using Nethereum.Util;
using Nethereum.Web3;
using WebSocket4Net;

namespace BatBot.Server.Services
{
    public sealed class BlocknativeMonitoringService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly SettingsOptions _settingsOptions;
        private readonly BlocknativeMessageService _blocknativeMessageService;

        public BlocknativeMonitoringService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, IOptionsFactory<SettingsOptions> settingsOptionsFactory, BlocknativeMessageService blocknativeMessageService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _settingsOptions = settingsOptionsFactory.Create(Options.DefaultName);
            _blocknativeMessageService = blocknativeMessageService;
        }

        public async Task<HttpResponseMessage> AddContractToWebhook()
        {
            using var client = GetBlocknativeClient();
            return await client.PostAsJsonAsync("address", GetAddressMessage());
        }

        public async Task<HttpResponseMessage> RemoveContractFromWebhook()
        {
            using var client = GetBlocknativeClient();
            return await client.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(new Uri(_batBotOptions.BlocknativeApiHttpUrl), "address"),
                Content = new StringContent(JsonSerializer.Serialize(GetAddressMessage()), Encoding.UTF8, MediaTypeNames.Application.Json)
            });
        }

        private HttpClient GetBlocknativeClient()
        {
            var client = new HttpClient {BaseAddress = new Uri(_batBotOptions.BlocknativeApiHttpUrl) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_batBotOptions.BlocknativeUsername}:{_batBotOptions.BlocknativePassword}")));
            return client;
        }

        private AddressMessage GetAddressMessage()
        {
            return new AddressMessage
            {
                ApiKey = _batBotOptions.BlocknativeApiKey,
                Address = _batBotOptions.ContractAddress,
                Blockchain = _batBotOptions.Blockchain,
                Networks = new Collection<string> {_batBotOptions.Network}
            };
        }

        public async Task Subscribe(CancellationToken cancellationToken)
        {
            var ws = new WebSocket(_batBotOptions.BlocknativeApiWssUrl);

            ws.Opened += (sender, e) =>
            {
                var serialize = JsonSerializer.Serialize(GetInitializeMessage());
                ws.Send(serialize);
            };

            ws.MessageReceived += async (sender, e) =>
            {
                var document = JsonDocument.Parse(e.Message);

                if (document.RootElement.GetProperty(Blocknative.Properties.Status).ValueEquals(Blocknative.Statuses.Ok) && document.RootElement.TryGetProperty(Blocknative.Properties.Event, out var @event))
                {
                    var categoryCode = @event.GetProperty(Blocknative.Properties.CategoryCode);
                    if (categoryCode.ValueEquals(Blocknative.CategoryCodes.Initialize))
                    {
                        var deserializeOptions = new JsonSerializerOptions();
                        deserializeOptions.Converters.Add(new BigIntegerConverter());
                        var serialize = JsonSerializer.Serialize(GetConfigsMessage(), deserializeOptions);
                        ws.Send(serialize);
                    }
                    else if (categoryCode.ValueEquals(Blocknative.CategoryCodes.Configs))
                    {
                        var serialize = JsonSerializer.Serialize(GetWatchMessage());
                        ws.Send(serialize);
                    }
                    else if (categoryCode.ValueEquals(Blocknative.CategoryCodes.ActiveAddress))
                    {
                        await _blocknativeMessageService.Handle(@event.GetProperty(Blocknative.Properties.Transaction), @event.GetProperty(Blocknative.Properties.ContractCall), TransactionSource.BlocknativeWebSocket, cancellationToken);
                    }
                }
            };

            await ws.OpenAsync();

            InitializeMessage GetInitializeMessage() => GetWebSocketMessage<InitializeMessage>();

            ConfigsMessage GetConfigsMessage()
            {
                var configMessage = GetWebSocketMessage<ConfigsMessage>();
                configMessage.Config = new ConfigsMessage.ConfigJson
                {
                    Filters = new Collection<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            {
                                $"{Blocknative.Properties.ContractCall}.{Blocknative.Properties.MethodName}", Uniswap.SwapExactEthForTokens
                            }
                        }
                    },
                    Scope = _batBotOptions.ContractAddress
                };

                if (!_settingsOptions.YoloMode)
                {
                    configMessage.Config.Filters.Add(new Dictionary<string, object>
                    {
                        {
                            Blocknative.Properties.Value, new Dictionary<string, object> {{Blocknative.Filters.Gte, Web3.Convert.ToWei(_settingsOptions.MinimumTargetSwapEth)}}
                        }
                    });

                    configMessage.Config.Filters.Add(new Dictionary<string, object>
                    {
                        {
                            Blocknative.Properties.Value, new Dictionary<string, object> {{Blocknative.Filters.Lte, Web3.Convert.ToWei(_settingsOptions.MaximumTargetSwapEth)}}
                        }
                    });

                    configMessage.Config.Filters.Add(new Dictionary<string, object>
                    {
                        {
                            Blocknative.Properties.GasPrice, new Dictionary<string, object> {{Blocknative.Filters.Lte, Web3.Convert.ToWei(_settingsOptions.MaximumGasPrice, UnitConversion.EthUnit.Gwei)}}
                        }
                    });
                }

                return configMessage;
            }

            WatchMessage GetWatchMessage()
            {
                var watchMessage = GetWebSocketMessage<WatchMessage>();
                watchMessage.Account = new WatchMessage.AccountJson
                {
                    Address = _batBotOptions.ContractAddress
                };

                return watchMessage;
            }

            T GetWebSocketMessage<T>() where T : WebSocketMessage, new()
            {
                return new T
                {
                    DappId = _batBotOptions.BlocknativeApiKey,
                    Version = _batBotOptions.BlocknativeApiVersion,
                    Blockchain = new WebSocketMessage.BlockchainJson
                    {
                        System = _batBotOptions.Blockchain,
                        Network = _batBotOptions.Network
                    }
                };
            }
        }
    }
}
