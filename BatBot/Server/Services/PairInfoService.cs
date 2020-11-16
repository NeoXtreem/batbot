using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using BatBot.Server.Constants;
using BatBot.Server.Dtos.Graph;
using BatBot.Server.Functions;
using BatBot.Server.Helpers;
using BatBot.Server.Models;
using BatBot.Server.Models.Graph;
using Microsoft.Extensions.Options;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;

namespace BatBot.Server.Services
{
    public sealed class PairInfoService
    {
        private readonly BatBotOptions _batBotOptions;
        private readonly IMapper _mapper;
        private readonly SmartContractService _smartContractService;
        private readonly GraphService _graphService;

        private string _factoryAddress;
        private readonly Dictionary<(string, string), string> _pairAddresses = new Dictionary<(string, string), string>();

        public PairInfoService(IOptionsFactory<BatBotOptions> batBotOptionsFactory, IMapper mapper, SmartContractService smartContractService, GraphService graphService)
        {
            _batBotOptions = batBotOptionsFactory.Create(Options.DefaultName);
            _mapper = mapper;
            _smartContractService = smartContractService;
            _graphService = graphService;
        }

        public async Task<Pair> GetPair(IWeb3 web3, (string TokenA, string TokenB) tokenAddresses, CancellationToken cancellationToken = default)
        {
            _factoryAddress ??= await _smartContractService.ContractQuery<FactoryFunction, string>(web3, _batBotOptions.ContractAddress);

            if (!_pairAddresses.TryGetValue(tokenAddresses, out var pairAddress))
            {
                pairAddress = (await _smartContractService.ContractQuery<GetPairFunction, string>(web3, _factoryAddress, new GetPairFunction {TokenA = tokenAddresses.TokenA, TokenB = tokenAddresses.TokenB})).ToLowerInvariant();
                _pairAddresses.Add(tokenAddresses, pairAddress);
            }

            if (_batBotOptions.Network == BatBotOptions.Mainnet)
            {
                var idName = JsonHelper.GetJsonPropertyName<PairType>(nameof(PairType.Id));
                return pairAddress != Uniswap.InvalidAddress
                    ? _mapper.Map<Pair>((await _graphService.SendQuery<PairResponse>(
                        new Dictionary<string, string> {{idName, Graph.Types.Id}},
                        new Dictionary<string, object> {{idName, $"${idName}"} },
                        variables: new {id = pairAddress},
                        cancellationToken: cancellationToken)).Pair)
                    : null;
            }

            return new Pair
            {
                Token0 = await GetToken(tokenAddresses.TokenA),
                Token1 = await GetToken(tokenAddresses.TokenB)
            };

            async Task<Token> GetToken(string address) =>
                new Token
                {
                    Id = tokenAddresses.TokenB,
                    Decimals = await _smartContractService.ContractQuery<DecimalsFunction, int>(web3, address),
                    Symbol = await web3.Eth.GetContractQueryHandler<SymbolFunction>().QueryAsync<string>(address),
                    TotalSupply = await _smartContractService.ContractQuery<TotalSupplyFunction, BigInteger>(web3, address)
                };
        }
    }
}
