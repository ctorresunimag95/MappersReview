using MappersComparasion.Models;
using Riok.Mapperly.Abstractions;

namespace MappersComparasion.Mappers;

[Mapper]
public partial class MapperlyUserMapper
{
    [MapProperty("Address.City", nameof(UserDto.City))]
    [MapperIgnoreSource(nameof(User.BirthDate))]
    public partial UserDto Map(User src);
}
