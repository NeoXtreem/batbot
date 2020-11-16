using AutoMapper;
using BatBot.Server.Functions;
using BatBot.Server.Models;

namespace BatBot.Server.Profiles
{
    public class TransactionProfile : Profile
    {
        public TransactionProfile()
        {
            CreateMap<SwapExactEthForTokensFunction, Swap>()
                .ForMember(d => d.AmountIn, o => o.MapFrom(s => s.AmountToSend));

            CreateMap<SwapExactEthForTokensSupportingFeeOnTransferTokensFunction, Swap>()
                .IncludeBase<SwapExactEthForTokensFunction, Swap>();

            CreateMap<SwapExactTokensForEthFunction, Swap>()
                .ForMember(d => d.AmountIn, o => o.MapFrom(s => s.AmountIn));
            
            CreateMap<SwapExactTokensForEthSupportingFeeOnTransferTokensFunction, Swap>()
                .IncludeBase<SwapExactTokensForEthFunction, Swap>();
        }
    }
}
