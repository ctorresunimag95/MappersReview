using MappersComparasion.Mappers;
using MappersComparasion.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MappersComparasion.DI;

public static class ManualDIDemo
{
    public static void Run()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserMapper, ManualUserMapper>();

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IUserMapper>();

        var dto = mapper.Map(SampleData.CreateUser());
        Console.WriteLine($"  Mapped: Id={dto.Id}, Name={dto.FirstName} {dto.LastName}, City={dto.City}");
    }
}
