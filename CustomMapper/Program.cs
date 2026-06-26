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

var destinationAsync = await mapper.MapAsync<User, UserDto>(new User
{
    Id = 2,
    FirstName = "John",
    LastName = "Smith",
    BirthDate = new DateTime(1985, 6, 15),
    Address = new Address { Street = "456 Oak Ave", City = "Los Angeles", Country = "US" }
});

Console.WriteLine($"User dto async {destinationAsync}");

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

public class UserAsyncMapper : IAsyncMapperProfile<User, UserDto>
{
    public async Task<UserDto> MapAsync(User source, CancellationToken cancellationToken = default)
    {
        // Simulate async work (e.g., enriching from a DB or external service)
        await Task.Delay(1, cancellationToken);

        return new()
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            City = source.Address.City
        };
    }
}