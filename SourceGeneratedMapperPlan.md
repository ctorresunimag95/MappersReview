# Standalone Source-Generated Mapper Plan

## Overview

Build a new self-contained source-generator-based mapper library that can later be extracted into its own NuGet package. The package should own its runtime abstractions, `Mapper` attribute, incremental generator, and DI registration story. Consumer code authors a `partial` mapper class, and the generator emits injectable mapping implementations that copy exact-name and exact-type members and optionally invoke a user-authored `ExtendMap` hook for enrichment or custom assignments.

## Goals

1. Create a new class library that does not reference any existing project in this solution.
2. Support a `partial` mapper class decorated with `[Mapper]`.
3. Generate injectable mapper implementations at compile time.
4. Map only exact property names and exact property types in the first version.
5. Support an `ExtendMap` callback in the partial class for enrichment and custom mapping.
6. Keep the package extractable to NuGet without repo-specific coupling.

## Non-Goals For MVP

1. Nested flattening such as `Address.City -> City`.
2. Collection mapping.
3. Enum conversion.
4. Custom converters.
5. Null-substitution policies.
6. Interoperability with the existing `CustomMapper` project.

## Package Boundary

The new library must be self-owned.

1. It must not reference any project in this solution.
2. It must not depend on repo-specific models, services, or abstractions.
3. It should expose its own runtime contracts and generator attribute.
4. Any later bridge to the existing `CustomMapper` project should be a separate integration phase.

## Public API Shape

Recommended MVP runtime surface. **Critical design constraint:** every shipped runtime type below depends only on the `System` namespace (`Attribute`, `IServiceProvider`, `InvalidOperationException`). This is what lets the generator and the runtime contracts live in a *single* `netstandard2.0` assembly without dragging extra dependencies into the Roslyn compiler when it loads the analyzer.

```csharp
namespace CustomMapper.SourceGenerator;

[AttributeUsage(AttributeTargets.Class)]
public sealed class MapperAttribute : Attribute
{
}

// Facade resolved at the call site.
public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source);
}

// Per-pair contract implemented by each generated mapper class.
public interface IMapper<TSource, TDestination>
{
    TDestination Map(TSource source);
}

// Dispatches to the registered per-pair mapper; throws when none is registered.
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
```

Notes:
- `MapperImplementation` uses only `IServiceProvider.GetService` (in `System`), so the runtime surface stays MEDI-free — `Microsoft.Extensions.DependencyInjection` is referenced only by the *generated* `AddGeneratedMappers(...)` code, which compiles inside the consumer where MEDI already exists.
- Keep `IMapper<TSource, TDestination>` **invariant** (no `in`/`out`). The facade resolves the exact closed type from DI; variance buys nothing here and can cause ambiguous-resolution surprises.

Registration is provided through a generator-emitted `AddGeneratedMappers(...)` API (see DI section).

## Authoring Model

Consumers should write mapper classes like this:

```csharp
using CustomMapper.SourceGenerator;

[Mapper]
public partial class UserMapper
{
    private readonly IAuditService _auditService;

    public UserMapper(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public partial UserDto Map(User source);

    private void ExtendMap(User source, UserDto destination)
    {
        _auditService.Log($"Mapped user {source.Id}");
        destination.DisplayName = $"{source.FirstName} {source.LastName}";
    }
}
```

## Supported Mapping Rules For v1

1. Only support partial instance methods with exactly one source parameter and one destination return type.
2. Only auto-map writable destination properties.
3. Only map properties when the source and destination names match exactly.
4. Only map properties when the source and destination types are exactly the same.
5. Ignore destination properties that do not meet the rule and report them through diagnostics.

## ExtendMap Contract

`ExtendMap` is the intended extension point for enrichment and custom assignments.

1. The generator should look for `void ExtendMap(TSource source, TDestination destination)` declared on the mapper class, of **any accessibility** (`private` is the natural choice). It is a regular user-written method, not a partial the generator implements.
2. If present with the exact signature, call it after generated assignments and before returning the destination object. Injected services (constructor fields) are usable inside it.
3. If present with a name match but the wrong signature, report a diagnostic (likely a mistake) rather than silently ignoring it.
4. Resolve `ExtendMap` per mapping pair — a class with several `Map` methods may define one `ExtendMap` overload per `(source, destination)` pair.

## DI And Discovery

Generated mappers should be normal injectable services.

1. Constructor injection must be supported in the partial mapper class.
2. The package should provide a clear registration mechanism.
3. Preferred MVP direction: generate `AddGeneratedMappers(...)` into the consumer compilation for reflection-free registration.
4. The `IMapper` facade should resolve `IMapper<TSource, TDestination>` from DI and delegate the call.
5. Runtime reflection-based assembly scanning should not be part of the first version unless explicitly chosen later.

## Generator Responsibilities

The source generator should:

1. Detect `[Mapper]` on partial classes.
2. Detect supported partial mapping methods.
3. Emit method bodies for valid methods.
4. Ensure generated mapper types satisfy the appropriate `IMapper<TSource, TDestination>` contracts (emit `partial class X : IMapper<TSrc,TDest>` in the generated partial so the contract is added without the user restating it).
5. Emit fully qualified type names (`global::`) to avoid namespace collisions.
6. Emit registration support for the discovered mappers.

### Incremental-generator correctness notes

Implement as `IIncrementalGenerator` (not the legacy `ISourceGenerator`) and follow the caching rules — getting these wrong silently breaks incrementality or causes generator exceptions:

1. Use `context.SyntaxProvider.ForAttributeWithMetadataName("CustomMapper.SourceGenerator.MapperAttribute", predicate, transform)` — far cheaper than a hand-rolled `CreateSyntaxProvider` scan.
2. The `transform` must project symbols into a small **equatable** value model (`record` / `record struct` of strings + small lists). **Never** store `ISymbol`, `Compilation`, or syntax nodes in pipeline state — they defeat caching and pin compilations in memory. Use a `value`-equality wrapper for any collection in the model.
3. For property matching, resolve names/types during `transform` (e.g. `Compilation.ClassifyConversion(src, dest).IsImplicit` or exact `SymbolEqualityComparer` equality per the v1 rule), and store the resulting `(destProp, srcProp)` name pairs — not the symbols.
4. Emit per-class partials with `context.RegisterSourceOutput`.
5. Emit the single `AddGeneratedMappers` file from `provider.Collect()` + one `RegisterSourceOutput`, so all discovered mappers land in one registration method.
6. Build source with a plain `StringBuilder`/`SourceBuilder` and `AddSource(hintName, SourceText.From(code, Encoding.UTF8))`; give each file a stable, unique `hintName` (e.g. `UserMapper.g.cs`, `GeneratedMapperRegistration.g.cs`).

### Example generated output

For the `UserMapper` in the authoring model, the generator emits roughly:

```csharp
// <auto-generated/>
#nullable enable
namespace App.Mappers
{
    partial class UserMapper : global::CustomMapper.SourceGenerator.IMapper<global::App.User, global::App.UserDto>
    {
        public partial global::App.UserDto Map(global::App.User source)
        {
            var destination = new global::App.UserDto();
            destination.Id = source.Id;
            destination.FirstName = source.FirstName;
            destination.LastName = source.LastName;
            ExtendMap(source, destination);   // emitted only when a matching ExtendMap exists
            return destination;
        }
    }
}
```

And one registration file per assembly:

```csharp
public static class GeneratedMapperRegistration
{
    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedMappers(
        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,
        global::Microsoft.Extensions.DependencyInjection.ServiceLifetime lifetime
            = global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)
    {
        services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(
            typeof(global::CustomMapper.SourceGenerator.IMapper<global::App.User, global::App.UserDto>),
            typeof(global::App.Mappers.UserMapper), lifetime));
        // ...one Add(...) per discovered (source,destination) pair...

        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
            .TryAddTransient<global::CustomMapper.SourceGenerator.IMapper,
                             global::CustomMapper.SourceGenerator.MapperImplementation>(services);
        return services;
    }
}
```

## Diagnostics

Diagnostics should be part of the MVP rather than deferred.

1. Error when `[Mapper]` is applied to a non-partial class.
2. Error when a mapping method has an unsupported signature.
3. Warning when destination properties are not mapped automatically.
4. Warning or error when `ExtendMap` exists with an invalid signature.
5. Optional informational diagnostic for generated mapper registration details.

## Suggested Project Structure

```text
CustomMapper.SourceGenerator/
  CustomMapper.SourceGenerator.csproj
  MapperAttribute.cs
  Abstractions.cs
  MapperImplementation.cs
  MapperGenerator.cs
  MapperModel.cs
  SourceBuilder.cs
```

The exact file split can vary, but the project should stay isolated from the rest of the solution.

## Project Setup (.csproj) And NuGet Packaging

A Roslyn generator must target `netstandard2.0` and be loadable as an analyzer. Because we ship the runtime contracts in the *same* assembly (single-project decision), the package must place that one DLL in **both** `lib/netstandard2.0` (so consumers can reference `IMapper`, `MapperImplementation`, `[Mapper]`) **and** `analyzers/dotnet/cs` (so the compiler runs the generator). This is the Riok.Mapperly packaging pattern.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <IsRoslynComponent>true</IsRoslynComponent>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>

    <!-- Ship runtime contracts in lib/ AND keep Roslyn out of the consumer's runtime closure -->
    <IncludeBuildOutput>true</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
  </PropertyGroup>

  <ItemGroup>
    <!-- 4.14.x matches the Roslyn shipped with the .NET 10 SDK; PrivateAssets=all = compile-only -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" PrivateAssets="all" />
  </ItemGroup>

  <!-- Pack the same DLL into the analyzer path so the compiler loads the generator -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>
</Project>
```

**In-repo consumption** (the sample project, via ProjectReference rather than NuGet) — `OutputItemType="Analyzer"` with `ReferenceOutputAssembly` at its default `true` makes the project act as both generator and normal reference:

```xml
<ProjectReference Include="..\CustomMapper.SourceGenerator\CustomMapper.SourceGenerator.csproj"
                  OutputItemType="Analyzer" />
```

**Escape hatch:** if the runtime surface ever needs to depend on MEDI (or anything beyond `System`) directly, split a `CustomMapper.SourceGenerator.Abstractions` (`netstandard2.0`/`net*`) project for the runtime types and keep the generator project analyzer-only. Two assemblies, two package paths, no shared-DLL trick. Not needed for the MVP.

## Implementation Phases

### Phase 1: Package Boundary And Contracts

1. Create the standalone project.
2. Define `[Mapper]`, `IMapper`, and `IMapper<TSource, TDestination>`.
3. Keep zero dependencies on this solution's internal libraries.

### Phase 2: Basic Generator

1. Detect `[Mapper]` partial classes.
2. Detect supported mapping methods.
3. Emit method bodies for exact-name and exact-type property mapping.
4. Invoke `ExtendMap` when present.

### Phase 3: DI Registration

1. Emit or provide the registration mechanism.
2. Ensure generated mappers are injectable.
3. Support constructor injection.

### Phase 4: Diagnostics And Validation

1. Add invalid-shape diagnostics.
2. Add unmapped-property diagnostics.
3. Confirm generated output is package-clean and repo-independent.

### Phase 5: Consumer Sample In This Repo

1. Add one minimal sample consumer.
2. Prove end-to-end generation, registration, and runtime mapping.
3. Add one `ExtendMap` example using an injected service or manual enrichment.

## Validation Strategy

1. Build the new package in isolation and confirm it has no project references to other solution libraries.
2. Add a minimal consumer sample with flat source and destination models that have exact matching property names and types.
3. In the consumer, set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` and inspect `obj/.../generated/CustomMapper.SourceGenerator/` to confirm the emitted `Map` body and `AddGeneratedMappers`.
4. Confirm the partial mapping method compiles without manual implementation (a "partial method has no implementation" error means the generator did not run / did not match).
5. Resolve the generated mapper through the `IMapper` facade and verify it maps a populated instance correctly; resolve an unregistered pair and assert `MapperImplementation` throws `InvalidOperationException`.
6. Verify constructor-injected services can be used inside `ExtendMap`.
7. Re-run with `AddGeneratedMappers(ServiceLifetime.Transient)` and confirm the lifetime override takes effect (two resolutions yield distinct instances).
8. Add negative validation cases for invalid class shapes and invalid method signatures (expect diagnostics, not crashes).
9. Inspect generated output and confirm there are no repo-specific namespaces or dependencies.
10. (Optional) Add a snapshot/unit test project using `CSharpGeneratorDriver` to lock the emitted source against regressions.

## Important Scope Decision

Do not use the current `User -> UserDto` pair in this repository as the first proof of the generator, because it already implies flattening behavior such as `Address.City -> City`. Start with flat models for the first end-to-end validation, then add more advanced scenarios later.

## Future Extensions

These can be added after the MVP is stable:

1. Nested or flattened member mapping.
2. Collection mapping.
3. Custom conversion hooks.
4. Enum conversion.
5. Null-handling policies.
6. Adapter integration with the existing `CustomMapper` project.

## Summary

The correct first version is a narrow, standalone, package-ready mapper source generator. It should generate injectable mappers from partial classes, map exact-name and exact-type properties only, and rely on an `ExtendMap` hook for enrichment. Package isolation is more important than immediate integration with the existing solution abstractions.