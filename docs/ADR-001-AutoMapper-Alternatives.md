# ADR-001: AutoMapper Library Alternatives

| Field | Value |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-06-17 |
| **Author** | Camilo Torres |
| **ADR Number** | |

---

## Context and Problem Statement

Object mapping — transforming domain entities into DTOs and back — is a cross-cutting concern present in virtually every .NET service layer. The choice of mapping strategy affects steady-state performance, memory allocation, startup cost, debugging transparency, dependency posture, and long-term maintainability.

This ADR documents the decision reached after benchmarking five mapping approaches under identical conditions on .NET 10 and evaluating them across both technical and organisational criteria.

The benchmark data used here reflects warm, steady-state in-memory mapping only. Startup, configuration, first-call latency, and IQueryable projection costs were evaluated separately as decision factors, but were not measured in the timed benchmark.

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
| **Mapster** | Runtime IL-emit library with an `IMapper`-compatible interface and lowest single-object allocation in the benchmark. |
| **Mapperly** | Compile-time source generator; no runtime dependency, compile-time mapping validation, performance statistically equal to manual. |
| **Manual mapping + Custom wrapper** | Handwritten assignments behind a thin `IMapper` / `IMapperProfile<TSource, TDestination>` abstraction with DI auto-registration via manual assembly discovery. |

### AutoMapper

- Mapping approach: runtime configuration with expression-tree-based mapping delegates compiled after startup configuration.
- Slowest measured option: 2.4× baseline at 1 object, 1.3× at 1,000 objects.
- Allocates 15–22% more memory than manual mapping at production scale (100+ objects), though it is lower than the manual baseline for the single-object case in this benchmark.
- Transitioning to a paid model, introducing licensing overhead and procurement dependency.
- Retained here only because familiarity and ecosystem reach may factor into some teams' evaluation.

### Mapster

- Mapping approach: runtime generation of cached mapping delegates from configured type maps.
- Runtime performance is within noise of manual mapping at production scale (99% of baseline at 1,000 objects).
- Lowest single-object allocation (112 B) of any candidate.
- Requires runtime configuration, adding startup cost outside the scope of this benchmark.
- External package: subject to standard package-support review.

### Mapperly

- Mapping approach: compile-time source generation that emits ordinary C# mapping code during the build.
- Compile-time source generator: no runtime library, no reflection, no warm-up.
- Statistically tied with manual mapping at production scale; fastest option at 100 objects.
- Provides compile-time validation — unmapped or incompatible members surface as build errors.
- Strongest alternative when reducing mapping regressions from model churn is a higher priority than fully owning handwritten mapper code.
- Introduces a build-time tooling dependency (Roslyn analyzer/generator).

### Manual Mapping + Custom Wrapper

- Performance baseline: all other projection approaches are measured against it.
- Fully owned internally; no external dependencies beyond `Microsoft.Extensions.DependencyInjection`.
- The `IMapperProfile<TSource, TDestination>` contract makes each mapping a plain, testable C# class.
- The `IMapper.Map<TSource, TDestination>` surface mirrors the AutoMapper API, reducing migration friction.
- Auto-registration via `AddMappers<TAssemblyMarker>()` uses manual assembly discovery (`typeof(TAssemblyMarker).Assembly.GetTypes()`) to locate all `IMapperProfile<,>` implementations and registers them without manual wiring.
- Mapping code is fully debuggable: a developer can step from the call site into the exact assignments.

---

## Decision Outcome

**Chosen option: Manual Mapping with the Custom Mapper wrapper.**

The custom mapper library provides a thin, DI-friendly abstraction layer over plain handwritten mapping code. It exposes an `IMapper` interface that mirrors the familiar AutoMapper contract, backed by strongly-typed `IMapperProfile<TSource, TDestination>` implementations that are auto-discovered and registered through standard reflection (`Assembly.GetTypes()`). Each profile is a regular C# class containing explicit property assignments — no reflection at runtime, no code generation tooling at build time, and no third-party library in the critical mapping path.

This approach achieves performance at the manual baseline (the top tier in the benchmark), eliminates external library risk from the mapping layer, and keeps every mapping behaviour fully visible, debuggable, and refactor-friendly. The `AddMappers<TAssemblyMarker>()` extension method reduces registration boilerplate to a single line using standard reflection (`Assembly.GetTypes()`) with no third-party scanning dependency, and the familiar `IMapper.Map<TSource, TDestination>()` call site minimises the surface change for teams migrating from AutoMapper.

For collection mapping, the wrapper intentionally keeps the core API object-oriented rather than adding a bulk-mapping abstraction. Small collections can be mapped with LINQ over `IMapper`, but large or performance-sensitive loops should inject the concrete `IMapperProfile<TSource, TDestination>` directly to avoid per-item scope creation overhead.

### Positive Consequences

- No licensing or procurement dependency on AutoMapper or any mapping-specific library.
- Mapping logic lives in the main codebase; it is owned, versioned, and evolved by the team.
- Full IDE support: rename, go-to-definition, and type-checking work on mapping code as they do for any other C#.
- Consistent `IMapper` contract across services simplifies onboarding and cross-team code review.
- Each `IMapperProfile` is a unit-testable class with no framework setup required.

### Negative Consequences / Trade-offs

- Mapping code is handwritten: larger or more complex domain models require proportionally more code.
- No automatic convention-based mapping: every property must be mapped explicitly (mitigated by AI-assisted generation tooling — see `ManualMappingGuidance.md`).
- No additional dependencies beyond `Microsoft.Extensions.DependencyInjection`; assembly scanning is implemented with standard BCL reflection.

---

## System Architecture

The following diagram illustrates how the custom mapper components interact at runtime and through DI registration.

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Application / Service Layer                                             │
│                                                                          │
│   ctor(IMapper mapper)  ──────────────────────────────────────────────┐  │
│                                                                       │  │
│   mapper.Map<User, UserDto>(user)  ───────────────────────────────┐   │  │
└───────────────────────────────────────────────────────────────────┼───┼──┘
                                                                    │   │
                        ┌───────────────────────────────────────────┘   │
                        ▼                                               │
          ┌─────────────────────────┐                                   │
          │        IMapper          │  (CustomMapper library)           │
          │  Map<TSource, TDest>()  │◄──────────────────────────────────┘
          └────────────┬────────────┘
                       │  resolves via DI scope
                       ▼
          ┌─────────────────────────────────────┐
          │  IMapperProfile<TSource, TDest>     │  (CustomMapper library)
          │  Map(TSource source): TDest         │
          └────────────┬────────────────────────┘
                       │  implemented by
                       ▼
          ┌─────────────────────────────────────┐
          │  UserMapper : IMapperProfile        │  (Application code)
          │  < User, UserDto >                  │
          │  — explicit property assignments —  │
          └─────────────────────────────────────┘

  ┌─────────────────────────────────────────────────────────────┐
  │  DI Registration (startup)                                  │
  │                                                             │
  │  services.AddMappers<Program>()                             │
  │       │                                                     │
  │       ├─ Assembly.GetTypes() scans assembly of TAssemblyMarker │
  │       │   → discovers all IMapperProfile<,> implementations    │
  │       │   → registers each as its interface                    │
  │       │                                                     │
  │       └─ registers IMapper → Mapper (Transient)             │
  └─────────────────────────────────────────────────────────────┘
```

**Data flow at runtime:**

1. A service receives `IMapper` via constructor injection.
2. A call to `Map<TSource, TDestination>(source)` creates a DI scope and resolves the matching `IMapperProfile<TSource, TDestination>`.
3. The profile's `Map` method performs explicit property assignments and returns the destination object.
4. The scope is disposed; no persistent state is held by the mapper.

---

## More Information

- **[CustomMapper Usage Guide](./CustomMapper-UsageGuide.md)** — how to define profiles, register the mapper, and call `IMapper` from application code.
- **[AutoMapper Migration Guide](./AutoMapper-MigrationGuide.md)** — step-by-step instructions for migrating an existing service from AutoMapper to the custom manual mapper.
- **[BenchmarkAnalysis.md](../BenchmarkAnalysis.md)** — full benchmark results, per-candidate analysis, and the data underpinning the recommendation.
- **[ManualMappingGuidance.md](../ManualMappingGuidance.md)** — guidance on using AI tooling to generate manual mapper implementations efficiently.
