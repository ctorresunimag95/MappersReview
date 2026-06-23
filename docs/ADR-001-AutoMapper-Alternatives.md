# ADR-001: AutoMapper Library Alternatives

| Field | Value |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-06-17 |
| **Author** | Camilo Torres |
| **ADR Number** | |
| **Updated** | 2026-06-22 (with CustomSourceGenerator benchmark) |

---

## Context and Problem Statement

Object mapping — transforming domain entities into DTOs and back — is a cross-cutting concern present in virtually every .NET service layer. The choice of mapping strategy affects steady-state performance, memory allocation, startup cost, debugging transparency, dependency posture, and long-term maintainability.

This ADR documents the decision reached after benchmarking six mapping approaches under identical conditions on .NET 10 and evaluating them across both technical and organisational criteria.

The benchmark data used here reflects warm, steady-state in-memory mapping only. Startup, configuration, first-call latency, and IQueryable projection costs were evaluated separately as decision factors, but were not measured in the timed benchmark.

**Benchmark data:** Final benchmark run includes Manual, Mapster, AutoMapper, Mapperly, Facet, and CustomSourceGenerator at 1, 100, and 1000 object scales (see BenchmarkAnalysis.md for full results).

---

## Current Process (AS-IS)

Several services in the portfolio currently rely on **AutoMapper** as their primary object-mapping library. AutoMapper has historically been a popular choice in the .NET ecosystem due to its convention-based configuration and familiar `IMapper` interface.

However, AutoMapper has recently announced a transition toward a **commercial/paid licensing model** for some usage tiers. This introduces new cost, compliance, and procurement concerns for teams that previously treated it as a freely available open-source dependency. Combined with its measured performance characteristics — the slowest and highest-allocating option among the like-for-like projection approaches in the benchmark conducted for this evaluation — the continued adoption of AutoMapper warrants a formal review.

---

## Decision Drivers

- **License and dependency risk.** AutoMapper's move toward a paid model creates procurement friction and potential compliance issues in enterprise environments. Any mapping library adopted as a shared architectural standard must have a stable, predictable dependency posture.
- **Steady-state performance.** Benchmark results show AutoMapper costs 2.4× the manual baseline per single-object map and 1.3× at 1,000-object scale, with higher memory allocation than manual mapping at production-scale workloads.
- **Developer experience.** The chosen approach must be straightforward to adopt, debug, and extend. Convention-based magic that hides the mapping path increases the cost of diagnosing unexpected output.
- **DI integration.** The approach must integrate naturally with `Microsoft.Extensions.DependencyInjection` without additional ceremony.
- **Consistency.** A shared wrapper keeps the `IMapper` contract familiar across services while decoupling them from any single third-party library.
- **Maintainability.** Mapping logic must be refactor-friendly; rename operations and type-checking tools should work on the mapping code as they do for any other handwritten C#.

---

## Considered Options

| Option | Description |
|--------|-------------|
| **Continue with AutoMapper** | Keep the existing dependency and accept the performance, licensing, and maintenance trade-offs. |
| **Mapster** | Runtime IL-emit library with an `IMapper`-compatible interface and fastest single-object performance. |
| **Mapperly** | Compile-time source generator; no runtime dependency, compile-time mapping validation. Best external library option by benchmark. |
| **Manual mapping + Custom wrapper** | Handwritten assignments behind a thin `IMapper` / `IMapperProfile<TSource, TDestination>` abstraction with DI auto-registration via manual assembly discovery. |
| **CustomSourceGenerator** | Custom in-house source generator emitting handwritten-like mapping code at compile time. No runtime dependencies beyond DI; performance exceeds manual baseline at scale. |

### AutoMapper

- Mapping approach: runtime configuration with expression-tree-based mapping delegates compiled after startup configuration.
- Slowest measured option: 1.89× baseline at 1 object, 1.38× at 100 objects, 1.52× at 1,000 objects.
- Allocates 22% more memory than manual mapping at production scale (1,000 objects).
- Transitioning to a paid model, introducing licensing overhead and procurement dependency.
- Retained here only because familiarity and ecosystem reach may factor into some teams' evaluation.

### Mapster

- Mapping approach: runtime generation of cached mapping delegates from configured type maps.
- Fastest single-object performance (0.91×), but regresses at scale: 0.99× at 100 objects, 1.25× at 1,000 objects.
- Lowest single-object allocation (112 B) of any candidate; matches manual at 100+ objects.
- Requires runtime configuration, adding startup cost outside the scope of this benchmark.
- External package: subject to standard package-support review.

### Mapperly

- Mapping approach: compile-time source generation that emits ordinary C# mapping code during the build.
- Compile-time source generator: no runtime library, no reflection, no warm-up.
- Performance: 0.97× at 1 object, 0.89× at 100 objects (tied for best external library), 1.17× at 1,000 objects.
- Provides compile-time validation — unmapped or incompatible members surface as build errors.
- Best third-party alternative for teams that want mature, externally-supported tooling.
- Introduces a build-time tooling dependency (Roslyn analyzer/generator).

### Manual Mapping + Custom Wrapper

- Performance baseline: 1.02–1.04× across all scales (steady-state).
- Fully owned internally; no external dependencies beyond `Microsoft.Extensions.DependencyInjection`.
- The `IMapperProfile<TSource, TDestination>` contract makes each mapping a plain, testable C# class.
- The `IMapper.Map<TSource, TDestination>` surface mirrors the AutoMapper API, reducing migration friction.
- Auto-registration via `AddMappers<TAssemblyMarker>()` uses manual assembly discovery (`typeof(TAssemblyMarker).Assembly.GetTypes()`) to locate all `IMapperProfile<,>` implementations and registers them without manual wiring.
- Mapping code is fully debuggable: a developer can step from the call site into the exact assignments.
- Requires handwritten mapping code for each type pair.

### CustomSourceGenerator

- Mapping approach: in-house compile-time source generator emitting strongly-typed mapping code.
- **Performance: 0.97× at 1 object, 0.77× at 100 objects (fastest), 1.01× at 1,000 objects (fastest, tied with manual).**
- Allocation is identical to manual at production scale; slightly higher at single object (248 B vs 184 B manual).
- Fully owned internally; no external dependencies beyond `Microsoft.Extensions.DependencyInjection` and standard Roslyn analyzers (build-time only).
- `[Mapper]` attribute on partial classes; `Map` method signature defined in source, implementation auto-generated.
- Optional `ExtendMap` hook allows post-generation customization per mapping (e.g., nested property flattening).
- Generated code is readable, debuggable, and follows manual mapping patterns exactly.
- Zero startup cost: no runtime IL emission, no reflection, no configuration overhead.
- **Eliminates the handwriting burden of manual mapping while maintaining full ownership and visibility.**
- Best performance at scale and lowest barrier to adoption for teams building internal tooling.

---

## Decision Outcome

Two distinct migration paths apply depending on whether the service already uses AutoMapper or is being built from scratch.

---

### Path A — Existing projects migrating from AutoMapper

**Primary: CustomSourceGenerator → Fallback: Mapperly**

Services currently on AutoMapper should migrate to **CustomSourceGenerator** first. The `[Mapper]` attribute pattern requires minimal onboarding change: developers write the method signature, the generator produces the implementation, and the `AddMappers<TAssemblyMarker>()` DI registration replaces `AddAutoMapper(...)` with a single line change. Generated code is readable, debuggable, and free of external runtime dependencies — addressing both the licensing concern and the performance gap directly.

If the team is unable or unwilling to take a dependency on the in-house source generator infrastructure (e.g., limited Roslyn tooling support, shared-service constraints), **Mapperly** is the fallback. It provides an identical compile-time approach, mature external support, and compile-time mapping validation, at a modest performance cost (0.89× vs 0.77× at 100 objects).

| Priority | Choice | Rationale |
|----------|--------|-----------|
| 1st | **CustomSourceGenerator** | Fastest at scale, zero runtime dependency, full ownership, drops AutoMapper license concern |
| 2nd | **Mapperly** | Mature external alternative when custom generator infrastructure is not available |

---

### Path B — New projects

**Primary: Manual mapping → Fallback: CustomSourceGenerator or Mapperly**

For greenfield services, **manual mapping** is the default starting point. It has no external dependencies, no build-time tooling requirements, predictable performance, and zero setup cost. Each mapping is a plain, testable C# class behind `IMapperProfile<TSource, TDestination>` — fully debuggable and refactor-friendly from day one.

As the mapping surface grows and handwriting burden becomes a concern, teams should escalate to **CustomSourceGenerator** (preferred) or **Mapperly** (external alternative) without changing the calling interface.

| Priority | Choice | Rationale |
|----------|--------|-----------|
| 1st | **Manual mapping** | No tooling dependencies, transparent code, minimal setup for new services |
| 2nd | **CustomSourceGenerator** | When boilerplate becomes a burden and the team can maintain the generator |
| 3rd | **Mapperly** | When external tooling is preferred over an in-house generator |

### For Collection Mapping

Both approaches intentionally keep the core API object-oriented rather than adding a bulk-mapping abstraction. Small collections can be mapped with LINQ, but large or performance-sensitive loops should inject the concrete `IMapperProfile<TSource, TDestination>` directly to avoid per-item scope creation overhead.

### Positive Consequences (CustomSourceGenerator)

- **Performance exceeds manual mapping at scale** (0.77× at 100 objects, 1.01× at 1,000 objects).
- Mapping code is auto-generated: no handwritten assignments, reducing boilerplate and human error.
- No external third-party library in the mapping path; dependencies are internal tooling only.
- Generated code is readable, debuggable, and follows manual mapping patterns exactly.
- Zero startup cost: no reflection, no IL emission, no runtime configuration.
- Consistent `IMapper` contract across services simplifies onboarding.
- Each generated mapper is testable via DI; no framework setup required.
- Full IDE support: generated code integrates seamlessly with rename, go-to-definition, and type-checking.

### Negative Consequences / Trade-offs (CustomSourceGenerator)

- Requires maintaining an in-house source generator (Roslyn-based tooling).
- Teams without prior source generator experience incur initial learning overhead.
- Customization beyond the `ExtendMap` hook requires editing the generator itself.

### Positive Consequences (Mapperly / External Alternative)

- Mature, well-maintained open-source tool with broad community adoption.
- Compile-time validation of all mappings; incompatibilities surface as build errors.
- Strong IDE integration and wide tool ecosystem support.
- Zero maintenance overhead: updates handled by upstream maintainers.
- Identical compile-time approach to CustomSourceGenerator, with proven stability.

### Negative Consequences / Trade-offs (Mapperly / External Alternative)

- External dependency subject to upstream maintenance and breaking changes.
- Performance at 1,000 objects (1.17×) lags CustomSourceGenerator (1.01×).
- Slightly higher single-object overhead vs. Mapster or manual baseline.

---

## System Architecture

### CustomSourceGenerator Approach

The following diagram illustrates how the CustomSourceGenerator mapper components interact at runtime and through DI registration.

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Application / Service Layer                                               │
│                                                                            │
│   [Mapper] partial class UserMapper { partial UserDto Map(User src); }     │
│                                                                            │
│   ctor(IMapper mapper)  ──────────────────────────────────────────────┐    │
│   mapper.Map<User, UserDto>(user)  ───────────────────────────────┐   │    │
└───────────────────────────────────────────────────────────────────┼───┼────┘
                                                                    │   │
                        ┌───────────────────────────────────────────┘   │
                        ▼                                               │
          ┌─────────────────────────────────────┐                       │
          │        IMapper                      │                       │
          │  Map<TSource, TDestination>()       │◄──────────────────────┘
          └────────────┬────────────────────────┘
                       │  resolves via DI scope
                       ▼
          ┌─────────────────────────────────────┐
          │  IMapperProfile<User, UserDto>      │  (Runtime interface)
          │  Map(User source): UserDto          │
          └────────────┬────────────────────────┘
                       │  implemented by
                       ▼
          ┌──────────────────────────────────────────────────────────┐
          │  [Generated] UserMapper.Map()                            │
          │  {                                                       │
          │    var dest = new UserDto();                             │
          │    dest.Id = source.Id;                                  │
          │    dest.FirstName = source.FirstName;                    │
          │    dest.City = source.Address.City;  // ExtendMap hook   │
          │    return dest;                                          │
          │  }                                                       │
          └──────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────┐
  │  Build-Time Source Generation                                    │
  │                                                                  │
  │  CustomSourceGenerator (Roslyn analyzer) scans [Mapper] classes  │
  │       │                                                          │
  │       ├─ reads partial method signatures                         │
  │       ├─ discovers ExtendMap hooks                               │
  │       ├─ generates full Map implementation                       │
  │       └─ emits .cs file to project                               │
  │                                                                  │
  │  No runtime reflection or IL emission                            │
  └──────────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────────┐
  │  DI Registration (startup)                                       │
  │                                                                  │
  │  services.AddMappers<Program>()                                  │
  │       │                                                          │
  │       ├─ Assembly.GetTypes() scans assembly of TAssemblyMarker   │
  │       │   → discovers all IMapperProfile<,> implementations      │
  │       │   → registers each as its interface                      │
  │       │                                                          │
  │       └─ registers IMapper → Mapper (Transient)                  │
  └──────────────────────────────────────────────────────────────────┘
```

**Data flow at runtime:**

1. A service receives `IMapper` via constructor injection.
2. A call to `Map<TSource, TDestination>(source)` resolves the matching `IMapperProfile<TSource, TDestination>` via DI.
3. The profile's `Map` method performs property assignments (generated code) and returns the destination object.
4. If an `ExtendMap` hook is defined, it is called to post-process the result.
5. No persistent state is held by the mapper.

### Mapperly Approach (External Alternative)

Mapperly works similarly: the `[Mapper]` partial class is decorated, the generator emits full methods at build time, and DI wiring is identical. The key difference is that Mapperly is maintained externally and provides additional compile-time validation of all mapped properties.

---

## More Information

- **[CustomSourceGenerator Documentation](../CustomMapper.SourceGenerator/)** — how to use the `[Mapper]` attribute, define `Map` methods, and implement `ExtendMap` hooks.
- **[Mapperly Documentation](https://mapperly.riok.app/)** — external resource for teams choosing Mapperly over a custom generator.
- **[AutoMapper Migration Guide](./AutoMapper-MigrationGuide.md)** — step-by-step instructions for migrating an existing service from AutoMapper to either CustomSourceGenerator or Mapperly.
- **[BenchmarkAnalysis.md](../BenchmarkAnalysis.md)** — full benchmark results across all six approaches (Manual, Mapster, AutoMapper, Mapperly, Facet, CustomSourceGenerator) at 1, 100, and 1,000 object scales.
- **Performance Summary:**
  - CustomSourceGenerator: 0.77× at 100 objects (fastest), 1.01× at 1,000 objects (fastest)
  - Mapperly: 0.89× at 100 objects, 1.17× at 1,000 objects (best external library)
  - Manual baseline: 1.00–1.04× across all scales
  - AutoMapper: 1.38–1.52× across scales (not recommended)
