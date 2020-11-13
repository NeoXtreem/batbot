using AutoMapper;
using BatBot.Server.Functions;
using BatBot.Server.Models;

namespace BatBot.Server.Profiles
{
    public class TransactionProfile : Profile
    {
        public TransactionProfile()
        {
            CreateMap<SwapExactEthForTokensFunction, Swap>();
        }
    }
}
