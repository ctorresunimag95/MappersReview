using MappersComparasion.Models;

namespace MappersComparasion.DI;

public static class SampleData
{
    public static User CreateUser(int id = 1) => new()
    {
        Id = id,
        FirstName = "Jane",
        LastName = "Doe",
        BirthDate = new DateTime(1990, 6, 15),
        Address = new Address { Street = "123 Main St", City = "New York", Country = "US" }
    };
}
