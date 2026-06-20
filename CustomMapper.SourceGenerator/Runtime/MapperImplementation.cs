using System;

namespace CustomMapper.SourceGenerator.Runtime
{
    /// <summary>
    /// Dispatches an <see cref="IMapper.Map{TSource, TDestination}"/> call to the
    /// registered <see cref="IMapper{TSource, TDestination}"/>; throws when none is registered.
    /// Depends only on <see cref="IServiceProvider"/> so the runtime surface stays MEDI-free.
    /// </summary>
    public sealed class MapperImplementation : IMapper
    {
        private readonly IServiceProvider _provider;

        public MapperImplementation(IServiceProvider provider) => _provider = provider;

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            if (_provider.GetService(typeof(IMapper<TSource, TDestination>)) is not IMapper<TSource, TDestination> mapper)
                throw new InvalidOperationException(
                    $"No mapper registered for {typeof(TSource)} -> {typeof(TDestination)}.");

            return mapper.Map(source);
        }
    }
}
