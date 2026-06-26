using System;
using Microsoft.Extensions.DependencyInjection;

namespace CustomMapper;

/// <summary>
/// Defines a mapper that can map objects from a source type to a destination type using registered mapping profiles.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Maps the source object to the destination object.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source object to map.</param>
    /// <returns>The mapped destination object.</returns>
    TDestination Map<TSource, TDestination>(TSource source);
}

/// <summary>
/// Implements the <see cref="IMapper"/> interface to provide mapping functionality using registered mapping profiles.
/// </summary>
public class Mapper : IMapper
{
    private readonly IServiceProvider _serviceProvider;

    public Mapper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Maps the source object to the destination object using the appropriate mapping profile registered in the service provider.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source object to map.</param>
    /// <returns>The mapped destination object.</returns>
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        using var scope = _serviceProvider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapperProfile<TSource, TDestination>>();

        return mapper.Map(source);
    }
}
