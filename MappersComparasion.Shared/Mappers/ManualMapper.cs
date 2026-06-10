using MappersComparasion.Models;

namespace MappersComparasion.Mappers;

public static class ManualMapper
{
    public static UserDto Map(User src) => new()
    {
        Id = src.Id,
        FirstName = src.FirstName,
        LastName = src.LastName,
        City = src.Address.City
    };
}

public interface IUserMapper
{
    UserDto Map(User src);
}

public class ManualUserMapper : IUserMapper
{
    public UserDto Map(User src) => ManualMapper.Map(src);
}
