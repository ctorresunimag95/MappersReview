using CustomMapper.SourceGenerator.Runtime;
using MappersComparasion.Models;

namespace MappersComparasion.Mappers;

[Mapper]
public partial class CustomSourceGeneratorUserMapper
{
    public partial UserDto Map(User source);

    private void ExtendMap(User source, UserDto destination)
    {
        destination.City = source.Address.City;
    }
}
