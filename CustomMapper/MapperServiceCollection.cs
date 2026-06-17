using Microsoft.Extensions.DependencyInjection;

namespace CustomMapper;

public static class MapperServiceCollection
{
    public static IServiceCollection AddMappers<TAssemblyMarker>(this IServiceCollection services
        , ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        services.Scan(scan => scan.FromAssembliesOf(typeof(TAssemblyMarker))

            .AddClasses(classes => classes
                .AssignableTo(typeof(IMapperProfile<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithLifetime(serviceLifetime)
        );

        services.AddTransient<IMapper, Mapper>();

        return services;
    }
}
