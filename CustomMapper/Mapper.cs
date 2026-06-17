using System;
using Microsoft.Extensions.DependencyInjection;

namespace CustomMapper;

public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source);
}

public class Mapper : IMapper
{
    private readonly IServiceProvider _serviceProvider;

    public Mapper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        using var scope = _serviceProvider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapperProfile<TSource, TDestination>>();

        return mapper.Map(source);
    }
}
