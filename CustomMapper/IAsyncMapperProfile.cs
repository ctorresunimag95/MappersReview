namespace CustomMapper;

/// <summary>
/// Defines an async mapping profile between a source type and a destination type.
/// </summary>
/// <typeparam name="TSource">The type of the source object.</typeparam>
/// <typeparam name="TDestination">The type of the destination object.</typeparam>
/// <example>
/// <code>
/// public class OrderAsyncMapper : IAsyncMapperProfile<Order, OrderDto>
/// {
///     public async Task<OrderDto> MapAsync(Order source, CancellationToken cancellationToken = default)
///     {
///         // Simulate async work (e.g., enriching from a DB or external service)
///         await Task.Delay(1, cancellationToken);
///         return new OrderDto
///         {
///             Id = source.Id,
///             Total = source.Total
///         };
///     }
/// }
/// </code>
/// </example>
public interface IAsyncMapperProfile<TSource, TDestination>
{
    /// <summary>
    /// Asynchronously maps the source object to the destination object.
    /// </summary>
    /// <param name="source">The source object to map.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the mapped destination object.</returns>
    Task<TDestination> MapAsync(TSource source, CancellationToken cancellationToken = default);
}
