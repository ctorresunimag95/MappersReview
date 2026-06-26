using Microsoft.Extensions.DependencyInjection;

namespace CustomMapper;

/// <summary>
/// Extension methods for registering mapping profiles and the mapper service in the dependency injection container.
/// </summary>
public static class MapperServiceCollection
{
    /// <summary>
    /// Registers all mapping profiles found in the assembly of the specified marker type and the mapper service in the dependency injection container.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">The type used to locate the assembly containing the mapping profiles.</typeparam>
    /// <param name="services">The service collection to which the mapping profiles and mapper service will be added.</param>
    /// <param name="serviceLifetime">The lifetime of the mapping profile services.</param>
    /// <returns>The updated service collection.</returns>
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
