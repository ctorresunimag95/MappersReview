## Overview

Introduces a lightweight, DI-native object mapping library (`CustomMapper`) as a manual alternative to convention-based mappers like AutoMapper and Mapster. The design avoids reflection-heavy configuration by expressing each mapping as an explicit, testable class (a _profile_) that implements a typed interface.

## Proposed Changes

**Core abstractions**
- `IMapperProfile<TSource, TDestination>` — synchronous mapping contract
- `IAsyncMapperProfile<TSource, TDestination>` — async mapping contract, for profiles that need to enrich data from external sources (DB, API, etc.)

**`Mapper` / `IMapper`**
- Resolves the correct profile from the DI container at call time using a child scope, so profiles can safely hold scoped dependencies
- Exposes both `Map<TSource, TDestination>` and `MapAsync<TSource, TDestination>` with `CancellationToken` support

**`MapperServiceCollection` extensions**
- `AddMappers<TAssemblyMarker>()` — scans a single assembly and registers all `IMapperProfile` and `IAsyncMapperProfile` implementations
- `AddMappers(IEnumerable<Type> markers)` — multi-assembly overload for projects split across multiple assemblies
- Configurable `ServiceLifetime` (defaults to `Scoped`); `IMapper` itself is always registered as `Transient`

**Unit tests** (`CustomMapper.Tests`)
- Sync and async happy-path mapping
- Missing-profile throws `InvalidOperationException`
- Cancelled token propagates `OperationCanceledException`
- Registration: lifetime variants (scoped/transient/singleton), multi-assembly marker, `IMapper` transient guarantee

## Verification Checklist

- [ ] `dotnet test CustomMapper.Tests` passes with no failures
- [ ] `AddMappers<T>()` registers profiles and `IMapper` correctly when called from a host startup
- [ ] A profile that injects a scoped service (e.g. `DbContext`) resolves without captive-dependency errors
- [ ] `MapAsync` respects cancellation (test: pre-cancelled token throws `OperationCanceledException`)
- [ ] No unregistered profile silently returns `null` — `InvalidOperationException` is thrown
- [ ] Multi-assembly scan registers profiles from all supplied marker assemblies
