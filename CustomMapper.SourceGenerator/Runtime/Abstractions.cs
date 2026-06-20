namespace CustomMapper.SourceGenerator.Runtime
{
    /// <summary>
    /// Facade resolved at the call site. Dispatches to the registered per-pair mapper.
    /// </summary>
    public interface IMapper
    {
        TDestination Map<TSource, TDestination>(TSource source);
    }

    /// <summary>
    /// Per-pair contract implemented by each generated mapper class.
    /// Kept invariant by design: the facade resolves the exact closed type from DI.
    /// </summary>
    public interface IMapper<TSource, TDestination>
    {
        TDestination Map(TSource source);
    }
}
