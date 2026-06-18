using AutoMapper;
using MappersComparasion.Models;

namespace MappersComparasion.Mappers;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.City, opt => opt.MapFrom(s => s.Address.City));
    }
}
