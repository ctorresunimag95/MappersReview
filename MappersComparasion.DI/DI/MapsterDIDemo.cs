using Mapster;
using MapsterMapper;
using MappersComparasion.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MappersComparasion.DI;

public static class MapsterDIDemo
{
    public static void Run()
    {
        var config = new TypeAdapterConfig();
        config.NewConfig<User, UserDto>()
            .Map(dest => dest.City, src => src.Address.City);

        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var dto = mapper.Map<UserDto>(SampleData.CreateUser());
        Console.WriteLine($"  Mapped via IMapper: Id={dto.Id}, Name={dto.FirstName} {dto.LastName}, City={dto.City}");

        // Static extension — no DI needed
        TypeAdapterConfig<User, UserDto>.NewConfig()
            .Map(d => d.City, s => s.Address.City);
        var dtoStatic = SampleData.CreateUser().Adapt<UserDto>();
        Console.WriteLine($"  Mapped via Adapt<>: Id={dtoStatic.Id}, City={dtoStatic.City}");
    }
}
