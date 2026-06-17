using CustomMapper;
using MappersComparasion.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddMappers<Program>();

using var serviceProvider = services.BuildServiceProvider();

var mapper = serviceProvider.GetRequiredService<IMapper>();

var destination = mapper.Map<User, UserDto>(new User
{
    Id = 1,
    FirstName = "Jane",
    LastName = "Doe",
    BirthDate = new DateTime(1990, 1, 1),
    Address = new Address { Street = "123 Main St", City = "New York", Country = "US" }
});

Console.WriteLine($"User dto {destination}");

public class UserMapper : IMapperProfile<User, UserDto>
{
    public UserDto Map(User source)
    {
        return new()
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            City = source.Address.City
        };
    }
}