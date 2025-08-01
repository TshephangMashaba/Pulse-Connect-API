using Pulse_Connect_API.DTO;
using Pulse_Connect_API.Models;
using Pulse_Connect_API.DTO;
using AutoMapper;
namespace Pulse_Connect_API
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User mapping
            CreateMap<UserRegistrationDTO, User>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName));
        }
    }

}