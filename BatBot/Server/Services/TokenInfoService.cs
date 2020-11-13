using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BatBot.Server.Models;
using BatBot.Server.Models.Graph;
using Microsoft.Extensions.Options;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;

namespace BatBot.Server.Services
{
    public sealed class TokenInfoService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly BlockchainService _blockchainService;
        private readonly GraphService _graphService;

        public TokenInfoService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, BlockchainService blockchainService, GraphService graphService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _blockchainService = blockchainService;
            _graphService = graphService;
        }

        public async Task<Token> GetToken(IWeb3 web3, string address, CancellationToken cancellationToken = default)
        {
            if (_batBotOptions.Network == BatBotOptions.Mainnet)
            {
                var token = (await _graphService.SendQuery<TokenResponse>(new {id = address.ToLowerInvariant()}, cancellationToken: cancellationToken)).Token;

                return new Token
                {
                    Address = token.Id,
                    Decimals = int.Parse(token.Decimals),
                    Symbol = token.Symbol,
                    TotalSupply = BigInteger.Parse(token.TotalSupply)
                };
            }

            return new Token
            {
                Address = address,
                Decimals = await _blockchainService.ContractQuery<DecimalsFunction, int>(web3, address),
                Symbol = await web3.Eth.GetContractQueryHandler<SymbolFunction>().QueryAsync<string>(address),
                TotalSupply = await _blockchainService.ContractQuery<TotalSupplyFunction, BigInteger>(web3, address)
            };
        }
    }
}
