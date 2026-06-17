using System;

namespace CustomMapper;

public interface IMapperProfile<TSource, TDestination>
{
    TDestination Map(TSource source);
}
