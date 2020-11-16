using System;
using System.Numerics;
using AutoMapper;
using BatBot.Server.Dtos.Graph;
using BatBot.Server.Models.Graph;
using Rationals;
using Swap = BatBot.Server.Models.Graph.Swap;

namespace BatBot.Server.Profiles
{
    public class GraphProfile : Profile
    {
        public GraphProfile()
        {
            CreateMap<string, BigInteger>().ConvertUsing(s => string.IsNullOrEmpty(s) ? default : BigInteger.Parse(s));
            CreateMap<string, Rational>().ConvertUsing(s => string.IsNullOrEmpty(s) ? default : Rational.ParseDecimal(s, default));
            CreateMap<string, DateTime>().ConvertUsing(s => string.IsNullOrEmpty(s) ? default : DateTimeOffset.FromUnixTimeSeconds(long.Parse(s)).UtcDateTime);
            CreateMap<TokenType, Token>();
            CreateMap<PairType, Pair>();
            CreateMap<TransactionType, Transaction>();
            CreateMap<SwapType, Swap>();
        }
    }
}
