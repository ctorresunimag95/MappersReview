using MappersComparasion.Mappers;
using MappersComparasion.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MappersComparasion.DI;

public static class MapperlyDIDemo
{
    public static void Run()
    {
        RunScenarioA();
        RunScenarioB();
    }

    private static void RunScenarioA()
    {
        Console.WriteLine("  -- Scenario A: basic registration --");

        var services = new ServiceCollection();
        services.AddSingleton<MapperlyUserMapper>();

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<MapperlyUserMapper>();

        var dto = mapper.Map(SampleData.CreateUser());
        Console.WriteLine($"  Mapped: Id={dto.Id}, Name={dto.FirstName} {dto.LastName}, City={dto.City}");
    }

    private static void RunScenarioB()
    {
        Console.WriteLine("  -- Scenario B: mapper with injected service --");

        var services = new ServiceCollection();
        services.AddSingleton<IAuditService, ConsoleAuditService>();
        services.AddSingleton<MapperlyUserMapperWithService>();

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<MapperlyUserMapperWithService>();

        var dto = mapper.MapWithAudit(SampleData.CreateUser());
        Console.WriteLine($"  Mapped: Id={dto.Id}, Name={dto.FirstName} {dto.LastName}, City={dto.City}");
    }
}
