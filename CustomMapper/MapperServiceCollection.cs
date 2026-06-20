using Microsoft.Extensions.DependencyInjection;

namespace CustomMapper;

public static class MapperServiceCollection
{
    public static IServiceCollection AddMappers<TAssemblyMarker>(this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        foreach (var type in typeof(TAssemblyMarker).Assembly.GetTypes())
        {
            if (type is not { IsClass: true, IsAbstract: false })
                continue;

            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapperProfile<,>))
                    services.Add(new ServiceDescriptor(i, type, serviceLifetime));
            }
        }

        services.AddTransient<IMapper, Mapper>();

        return services;
    }
}
