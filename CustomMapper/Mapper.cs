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

    /// <summary>
    /// Maps the source object to the destination object asynchronously.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source object to map.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the mapped destination object.</returns>
    Task<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
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
        ArgumentNullException.ThrowIfNull(source);
        var mapper = _serviceProvider.GetRequiredService<IMapperProfile<TSource, TDestination>>();

        return mapper.Map(source);
    }

    /// <summary>
    /// Maps the source object to the destination object using the appropriate async mapping profile registered in the service provider.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source object to map.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the mapped destination object.</returns>
    public async Task<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var mapper = _serviceProvider.GetRequiredService<IAsyncMapperProfile<TSource, TDestination>>();

        return await mapper.MapAsync(source, cancellationToken);
    }
}
