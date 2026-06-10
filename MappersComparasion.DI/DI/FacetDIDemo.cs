using Facet.Extensions;
using MappersComparasion.Mappers;
using MappersComparasion.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MappersComparasion.DI;

public static class FacetDIDemo
{
    public static void Run()
    {
        RunScenarioA();
        RunScenarioB();
    }

    private static void RunScenarioA()
    {
        Console.WriteLine("  -- Scenario A: generated constructor, no DI --");

        var user = SampleData.CreateUser();
        var dto = new FacetUserDto(user);
        Console.WriteLine($"  Mapped: Id={dto.Id}, Name={dto.FirstName} {dto.LastName}, City={dto.City}");

        var users = new[] { user, SampleData.CreateUser(2) };
        var dtos = users.SelectFacets<User, FacetUserDto>().ToList();
        Console.WriteLine($"  Collection: {dtos.Count} items mapped");
    }

    private static void RunScenarioB()
    {
        Console.WriteLine("  -- Scenario B: DI-wired wrapper --");

        var services = new ServiceCollection();
        services.AddSingleton<IAuditService, ConsoleAuditService>();
        services.AddSingleton<IUserFacetMapper, FacetUserMapperService>();

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IUserFacetMapper>();

        var dto = mapper.Map(SampleData.CreateUser());
        Console.WriteLine($"  Mapped: Id={dto.Id}, Name={dto.FirstName} {dto.LastName}, City={dto.City}");
    }
}

public interface IUserFacetMapper
{
    FacetUserDto Map(User src);
}

public class FacetUserMapperService(IAuditService auditService) : IUserFacetMapper
{
    public FacetUserDto Map(User src)
    {
        var dto = new FacetUserDto(src);
        auditService.Log($"Facet mapped user {src.Id}");
        return dto;
    }
}
