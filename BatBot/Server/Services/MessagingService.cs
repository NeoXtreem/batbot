using System;
using System.Threading.Tasks;
using BatBot.Server.Hubs;
using BatBot.Server.Models;
using BatBot.Server.Types;
using BatBot.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BatBot.Server.Services
{
    public sealed class MessagingService
    {
        private const string ReceiveTxMessageMethod = "ReceiveTxMessage";
        private const string ReceiveLogMessageMethod = "ReceiveLogMessage";

        private readonly ILogger<MessagingService> _logger;
        private readonly IHubContext<MessageHub> _hub;
        private readonly BatBotOptions _batBotOptions;
        private MessageTier _messageTier = MessageTier.End;

        public MessagingService(ILogger<MessagingService> logger, IHubContext<MessageHub> hub, IOptionsFactory<BatBotOptions> batBotOptionsFactory)
        {
            _logger = logger;
            _hub = hub;
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
        }

        public async Task SendTxMessage(string message, MessageTier nextMessageTier = MessageTier.None) => await SendMessage(ReceiveTxMessageMethod, message, nextMessageTier);

        public async Task SendLogMessage(string message) => await SendMessage(ReceiveLogMessageMethod, message, MessageTier.None);

        private async Task SendMessage(string method, string message, MessageTier nextMessageTier)
        {
            // Treat the default None as continuing the same tier from the current tier state.
            if (nextMessageTier == MessageTier.None)
            {
                nextMessageTier = _messageTier;
            }

            var treeLine = _messageTier switch
            {
                MessageTier.End when nextMessageTier == MessageTier.Single => "┬─",
                MessageTier.End when nextMessageTier == MessageTier.Double => "┬┬",
                MessageTier.End when nextMessageTier == MessageTier.End => "──",
                MessageTier.Double when nextMessageTier == MessageTier.Double => "│├─",
                MessageTier.Double when nextMessageTier == MessageTier.End => "└┴─",
                MessageTier.Double when nextMessageTier == (MessageTier.End | MessageTier.Single) => "│└─",
                MessageTier.Single when nextMessageTier == MessageTier.Single || nextMessageTier == (MessageTier.End | MessageTier.Single) => "├─",
                MessageTier.Single when nextMessageTier == MessageTier.Double => "├┬",
                MessageTier.Single when nextMessageTier == MessageTier.End => "└─",
                _ => throw new InvalidOperationException($"{nextMessageTier} is an invalid next tier for current tier {_messageTier}.")
            };

            _messageTier = nextMessageTier == (MessageTier.End | MessageTier.Single) ? MessageTier.Single : nextMessageTier;

            var tieredMessage = message.Insert(0, treeLine);
            _logger.LogDebug(tieredMessage);
            await _hub.Clients.All.SendAsync(method, new Message {Timestamp = DateTime.Now, Text = method == ReceiveTxMessageMethod ? tieredMessage : message});
        }

        public Uri GetEtherscanUrl(string path) => new Uri(new Uri(_batBotOptions.EtherscanBaseUrl), path);
    }
}
