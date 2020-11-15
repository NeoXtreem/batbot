using System;
using System.Numerics;
using AutoMapper;
using BatBot.Server.Models;
using BatBot.Server.Models.Graph.Types;
using Rationals;

namespace BatBot.Server.Profiles
{
    public class GraphProfile : Profile
    {
        public GraphProfile()
        {
            CreateMap<string, BigInteger>().ConvertUsing(s => BigInteger.Parse(s));
            CreateMap<string, Rational>().ConvertUsing(s => Rational.ParseDecimal(s, default));
            CreateMap<string, DateTime>().ConvertUsing(s => DateTimeOffset.FromUnixTimeSeconds(long.Parse(s)).UtcDateTime);
            CreateMap<TokenType, Token>();
            CreateMap<PairType, Pair>();
        }
    }
}
