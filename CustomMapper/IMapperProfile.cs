using System;

namespace CustomMapper;

/// <summary>
/// Defines a mapping profile between a source type and a destination type.
/// </summary>
/// <typeparam name="TSource">The type of the source object.</typeparam>
/// <typeparam name="TDestination">The type of the destination object.</typeparam>
/// <example>
/// <code>
/// public class OrderMapper : IMapperProfile<Order, OrderDto>
/// {
///     public OrderDto Map(Order source)
///     {
///         return new OrderDto
///         {
///             Id = source.Id,
///             Total = source.Total
///         };
///     }
/// }
/// 
/// // Usage
/// var orderMapper = new OrderMapper();
/// var orderDto = orderMapper.Map(new Order { Id = 1, Total = 100 });
/// </code>
/// </example>
public interface IMapperProfile<TSource, TDestination>
{
    /// <summary>
    /// Maps the source object to the destination object.
    /// </summary>
    /// <param name="source">The source object to map.</param>
    /// <returns>The mapped destination object.</returns>
    TDestination Map(TSource source);
}
