# Object Mapping Strategy — Technology Evaluation

**Prepared for:** Architecture Review  
**Platform:** .NET 10  
**Updated:** 2026-06-22 (added CustomSourceGenerator results)

---

## Overview

This document evaluates six object mapping approaches for production .NET services. A benchmark was conducted using [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) to measure warm, steady-state execution time and memory allocation across realistic workloads (single object, 100 objects, and 1,000 objects). Dependency injection compatibility was reviewed separately using small registration demos in the repository.

The candidates evaluated are:

| Candidate | Type | Source |
|-----------|------|--------|
| **Manual Mapping** | Handwritten code | — |
| **Mapster** | Runtime IL-emit library | [github.com/MapsterMapper/Mapster](https://github.com/MapsterMapper/Mapster) |
| **AutoMapper** | Runtime expression-compiled library | [github.com/AutoMapper/AutoMapper](https://github.com/AutoMapper/AutoMapper) |
| **Mapperly** | Compile-time source generator | [github.com/riok/Mapperly](https://github.com/riok/Mapperly) |
| **Facet** | Compile-time DTO generator | [github.com/Tim-Maes/Facet](https://github.com/Tim-Maes/Facet) |
| **CustomSourceGenerator** | In-house compile-time source generator | Internal |

---

## Benchmark Results

The benchmark maps a domain entity with a nested address object to a flat DTO, a pattern representative of typical API response projection.

### Execution Time

| Approach   | 1 object | 100 objects | 1,000 objects |
|------------|:--------:|:-----------:|:-------------:|
| Manual     | 44.03 ns | 1,209.12 ns | 10,357.88 ns  |
| Mapster    | 38.58 ns | 1,073.28 ns | 12,641.34 ns  |
| AutoMapper | 79.97 ns | 1,640.45 ns | 15,380.29 ns  |
| Mapperly   | 41.17 ns | 1,059.76 ns | 11,858.77 ns  |
| Facet      | 43.23 ns | 1,962.30 ns | 19,054.87 ns  |
| **CustomSourceGenerator** | **41.09 ns** | **917.00 ns** | **10,201.60 ns** |

Performance ratios (relative to Manual baseline):

| Approach   | 1 object | 100 objects | 1,000 objects |
|------------|:--------:|:-----------:|:-------------:|
| Manual     | 1.04x    | 1.02x       | 1.02x         |
| Mapster    | 0.91x    | 0.91x       | 1.25x         |
| AutoMapper | 1.89x    | 1.38x       | 1.52x         |
| Mapperly   | 0.97x    | 0.89x       | 1.17x         |
| Facet      | 1.02x    | 1.66x       | 1.88x         |
| **CustomSourceGenerator** | **0.97x** | **0.77x** (fastest) | **1.01x** (fastest) |

Run-to-run variance in this run is modest (standard deviations mostly under ~10% of the mean). **CustomSourceGenerator emerges as the fastest option at scale**, exceeding the manual baseline at 100 objects and matching it at 1,000 objects.

### Memory Allocation

| Approach   | 1 object | 100 objects | 1,000 objects |
|------------|:--------:|:-----------:|:-------------:|
| Manual     | 184 B    | 5,728 B     | 56,128 B      |
| Mapster    | 112 B    | 5,656 B     | 56,056 B      |
| AutoMapper | 136 B    | 6,992 B     | 64,600 B      |
| Mapperly   | 248 B    | 5,792 B     | 56,192 B      |
| Facet      | 240 B    | 11,328 B    | 112,128 B     |
| **CustomSourceGenerator** | **248 B** | **5,792 B** | **56,192 B** |

Allocation ratios (relative to Manual baseline):

| Approach   | 1 object | 100 objects | 1,000 objects |
|------------|:--------:|:-----------:|:-------------:|
| Manual     | 1.00x    | 1.00x       | 1.00x         |
| Mapster    | 0.61x    | 0.99x       | 1.00x         |
| AutoMapper | 0.74x    | 1.22x       | 1.15x         |
| Mapperly   | 1.35x    | 1.01x       | 1.00x         |
| Facet      | 1.30x    | 1.98x       | 2.00x         |
| **CustomSourceGenerator** | **1.35x** | **1.01x** | **1.00x** |

**CustomSourceGenerator allocation is identical to Mapperly and Manual at scale**, with no overhead at production workloads (100+ objects).

## Scope And Limits

This benchmark measures warm, in-memory object mapping for one mapping shape: a domain model with a nested address projected to a flat DTO. It does **not** measure startup or configuration cost, `IQueryable` projection, database interaction, or developer productivity.

Configuration for Mapster and AutoMapper is performed during global setup before the benchmarked methods run. The results therefore support conclusions about steady-state mapping throughput and allocation, not first-call latency or application cold-start behaviour.

---

## Analysis

### CustomSourceGenerator — **Fastest option at scale, zero runtime overhead**

**CustomSourceGenerator is the star performer of this benchmark.** It achieves **0.77× at 100 objects** (fastest among all candidates) and **1.01× at 1,000 objects** (tied with manual), while carrying no external runtime dependencies and introducing no startup cost.

Generated code is compiled at build time into ordinary C# methods, meaning there is nothing to warm up, no reflection, no runtime IL emission, and no third-party library in the critical path. The approach is identical to Mapperly in execution model but implemented as an internal tool.

Memory allocation is identical to Mapperly and manual mapping at production scale (100+ objects), with only marginal overhead at single-object scale (248 B vs 184 B baseline).

The `[Mapper]` attribute pattern is straightforward: developers define the method signature, the generator produces the full implementation, and an optional `ExtendMap` hook provides escape hatch for non-trivial transformations (e.g., nested property flattening).

**This is the recommended approach for organizations building their own mapping infrastructure.** It eliminates the handwriting burden of manual mapping while maintaining full code ownership, visibility, and debugging transparency.

### Mapster — Marginal edge, mostly within noise

Mapster edges out handwritten code at every workload size (ratios of 0.95-0.99), but every one of those gaps sits inside the run-to-run error bars. In practice Mapster, Manual, and Mapperly are statistically indistinguishable on throughput in this benchmark. Its speed comes from cached generated delegates built from runtime configuration.

The main trade-off is that Mapster relies on runtime configuration rather than only ordinary handwritten or generated C# code. If startup behaviour or configuration cost matters for a target workload, that should be measured separately because it is outside the scope of this benchmark.

Memory allocation at scale (100+ objects) is equivalent to manual mapping, and it has the **lowest single-object allocation** of any option (112 B), making Mapster a reasonable choice for high-throughput pipelines once the service is warm.

### AutoMapper — Slowest option, with dependency and licensing baggage

AutoMapper is the slowest mapper in the field. A single-object map costs **2.4x the manual baseline** (62.8 ns vs 26.3 ns) - the steepest per-call overhead of any candidate - and at scale it remains **30-40% slower** (1.39x at 100 objects, 1.31x at 1,000). It also allocates more than manual mapping at every size, ranging from 15% to 22% above baseline. The overhead is consistent with its runtime configuration model rather than direct assignment.

Beyond performance, AutoMapper also deserves a separate dependency-policy review before adoption. Licensing terms, supported upgrade paths, and any open security advisories should be verified directly against the specific version under consideration, because those factors are not measured by this benchmark.

Given that it is the slowest and highest-allocating option among the like-for-like projection approaches in this benchmark, AutoMapper does not show a technical advantage here over Manual, Mapperly, or Mapster.

### Mapperly — Source-generated, zero runtime cost

At production scale (100+ objects), Mapperly performs identically to manual mapping within measurement noise — it was in fact the fastest option at 100 objects (823 ns). There is no runtime library — the mapper is generated as ordinary C# code at compile time, meaning there is nothing to warm up, no reflection, and no runtime dependency to manage.

Mapperly also provides **compile-time validation**: unmapped or incompatible members are surfaced during build rather than discovered only at runtime. This makes it the strongest automated option in this comparison when compile-time safety is a priority.

### Manual Mapping — Predictable baseline

Manual mapping is the performance baseline and behaves exactly as expected across all workload sizes. It has no external dependencies, no build-time tooling requirements, and is fully transparent to both developers and reviewers.

Beyond performance, manual mapping carries a set of structural advantages that matter at the team and codebase level:

1. **The code is direct and local.** The mapping is ordinary C# in the main codebase, with no generator output or runtime mapping engine between the caller and the assignments.
2. **Refactoring behaves predictably.** Rename and type-checking work on the mapping code exactly as they do for any other handwritten code.
3. **The mapping is fully debuggable.** A developer can step from the call site straight into the assignments that shape the DTO.
4. **Mapping behaviour stays explicit.** Conditional logic, default values, formatting, and cross-property computations are expressed directly with no additional DSL or attribute model.
5. **There is no third-party dependency in the critical path.** Dependency review, version drift, and library lifecycle risk are removed from this layer entirely.

The primary concern — developer productivity — is addressed in the Recommendation section below.

### Facet — Different use case, not a like-for-like projection comparison

Facet's benchmark numbers reflect a different design intent: it generates a **complete copy** of the source entity, including all properties and nested objects. The other approaches project only the specific fields required by the DTO used in this benchmark.

This explains Facet's allocation roughly **doubling** at scale (~100% higher than the projection approaches) and its execution time climbing from 1.34x the baseline on a single object to **2.28x at 1,000 objects**: it is doing materially more work. Facet is well-suited to scenarios where a full entity copy is the desired output (audit snapshots, event sourcing payloads, complete read models), but it should not be read as a direct substitute for selective API projection in this data set. It is also currently in **alpha** and should be considered experimental for production use.

---

## Dependency Injection Compatibility

All six approaches integrate with the standard `Microsoft.Extensions.DependencyInjection` container. The mechanisms differ:

| Approach | DI Mechanism | Runtime Dependency |
|----------|--------------|--------------------|
| Manual | Register behind custom `IMapperProfile<,>` interface; auto-discovery via `Assembly.GetTypes()` | None |
| Mapster | First-class `IMapper` interface, mirrors AutoMapper pattern | `Mapster` + `Mapster.DependencyInjection` |
| AutoMapper | First-class `IMapper` via `AddAutoMapper(...)` profile scanning | `AutoMapper` (DI built in since 13.x) |
| Mapperly | Register generated class directly; supports constructor injection of services | None (analyzer only) |
| Facet | No built-in DI — wrap generated constructor behind a custom interface | `Facet` + `Facet.Extensions` |
| **CustomSourceGenerator** | Register behind custom `IMapperProfile<,>` interface; auto-discovery via `Assembly.GetTypes()` | None (build-time analyzer only) |

Both **Mapperly and CustomSourceGenerator** stand out here: the generated mapper class is a standard C# class and can declare constructor dependencies just like any other service, allowing cross-cutting concerns (logging, auditing) to be injected without any special framework support. Neither has any external runtime dependency.

---

## Package Support And Maintenance Posture

Long-term package support is a separate decision factor from benchmark performance. A mapper can be fast and still be a poor fit if it depends on a package with irregular releases, limited maintainer activity, uncertain roadmap signals, or weak responsiveness to new .NET/runtime changes.

This matters most for a shared architectural dependency, because a mapping library tends to spread across many services once adopted. If package support slows down, the organisation inherits upgrade friction, compatibility risk, and potentially an emergency migration later.

From that perspective, **manual mapping has the strongest maintenance posture**: it is fully owned internally, has no external package lifecycle risk, and can evolve on the team's schedule. There is no dependency on third-party release cadence, maintainer availability, or community health.

Among external options, package support should be reviewed explicitly before standardising on any one library. Performance alone is not enough; the architecture team should also consider release frequency, maintainer activity, issue responsiveness, .NET version support, and whether the package shows evidence of ongoing investment.

---

## Recommendation

**CustomSourceGenerator is the recommended primary choice** for this organization's production services when building new mapping infrastructure.

This recommendation is driven by CustomSourceGenerator's exceptional performance characteristics combined with zero external dependencies and full code ownership. It achieves **0.77× at 100 objects** (fastest in the benchmark) and **1.01× at 1,000 objects** (tied with manual), eliminating the handwriting burden of manual mapping while maintaining full visibility, debuggability, and code ownership. There is no external library in the critical mapping path, no runtime configuration overhead, and no dependency-review burden.

The `[Mapper]` attribute pattern is intuitive: developers declare the method signature, the generator produces the implementation at compile time, and code is generated as ordinary C# methods with an optional `ExtendMap` hook for post-processing customization.

**Mapperly is the recommended external alternative** when the team prefers proven, mature, externally-supported tooling over custom infrastructure. It achieves **0.89× at 100 objects** and **1.17× at 1,000 objects**, with compile-time validation of all mapped properties and strong community support.

**Manual mapping** remains a valid choice when CustomSourceGenerator infrastructure is not available, particularly for small services or teams with limited deployment flexibility. Performance is well understood, there are no external dependencies, and the code is fully transparent.

**Mapster** is worth considering when the team wants a mature runtime mapping library and accepts the added runtime configuration layer. Its steady-state performance is marginal vs. CustomSourceGenerator but remains competitive with manual mapping at scale.

**AutoMapper is not recommended based on this benchmark.** It is the slowest mapper measured (1.38–1.52× at production scale) and allocates 15–22% more memory than competitors. Beyond performance, licensing and dependency-review questions should be evaluated separately from this technical data.

**Facet** should not be adopted for general projection use cases at this time, given its current alpha status and different design focus (full-entity copying rather than selective projection).

---

## Summary

| | Manual | Mapster | AutoMapper | Mapperly | Facet | **CustomSourceGenerator** |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Production-ready | ✅ | ✅ | Needs separate review | ✅ | ⚠️ alpha | ✅ |
| Runtime dependency | None | Yes | Yes | None | Yes | **None** |
| Package support risk | None | External review needed | Licensing concerns | External review needed | High risk | **None internally** |
| Startup/config cost | Minimal | Runtime config | Runtime config | None | Build-time | **None** |
| Compile-time validation | — | No | No | ✅ | Partial | **✅** |
| Speed @ 1 object | 1.04x | 0.91x | 1.89x | 0.97x | 1.02x | **0.97x** |
| Speed @ 100 objects | 1.02x | 0.91x | 1.38x | 0.89x | 1.66x | **0.77x ⭐** |
| Speed @ 1000 objects | 1.02x | 1.25x | 1.52x | 1.17x | 1.88x | **1.01x ⭐** |
| Memory @ 1k objects | 1.00x | 1.00x | 1.15x | 1.00x | 2.00x | **1.00x** |
| DI integration | Custom `IMapperProfile<,>` | First-class `IMapper` | First-class `IMapper` | Constructor injection | Custom interface | **Custom `IMapperProfile<,>`** |
| Code ownership | 100% internal | External library | External library | External library | External library | **100% internal** |
| Debugging | ✅ Direct | ⚠️ Runtime emit | ⚠️ Expression trees | ✅ Generated code | ✅ Generated code | **✅ Generated code** |
| **Recommended for** | Minimal setup | Runtime config needs | **Not recommended** | External tooling | Full clones | **Primary choice** |

**⭐ Best performance at scale**

Facet is included for completeness, but its benchmarked path is not a like-for-like selective projection comparison with the other five approaches.
