# Object Mapping Strategy — Technology Evaluation

**Prepared for:** Architecture Review  
**Platform:** .NET 10  

---

## Overview

This document evaluates five object mapping approaches for production .NET services. A benchmark was conducted using [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) to measure warm, steady-state execution time and memory allocation across realistic workloads (single object, 100 objects, and 1,000 objects). Dependency injection compatibility was reviewed separately using small registration demos in the repository.

The candidates evaluated are:

| Candidate | Type | Source |
|-----------|------|--------|
| **Manual Mapping** | Handwritten code | — |
| **Mapster** | Runtime IL-emit library | [github.com/MapsterMapper/Mapster](https://github.com/MapsterMapper/Mapster) |
| **AutoMapper** | Runtime expression-compiled library | [github.com/AutoMapper/AutoMapper](https://github.com/AutoMapper/AutoMapper) |
| **Mapperly** | Compile-time source generator | [github.com/riok/Mapperly](https://github.com/riok/Mapperly) |
| **Facet** | Compile-time DTO generator | [github.com/Tim-Maes/Facet](https://github.com/Tim-Maes/Facet) |

---

## Benchmark Results

The benchmark maps a domain entity with a nested address object to a flat DTO, a pattern representative of typical API response projection.

### Execution Time

| Approach   | 1 object | 100 objects | 1,000 objects |
|------------|:--------:|:-----------:|:-------------:|
| Manual     | 26.3 ns  | 910 ns      | 8,390 ns      |
| Mapster    | 25.0 ns  | 857 ns      | 8,320 ns      |
| AutoMapper | 62.8 ns  | 1,253 ns    | 11,002 ns     |
| Mapperly   | 36.7 ns  | 823 ns      | 8,712 ns      |
| Facet      | 35.2 ns  | 1,365 ns    | 19,110 ns     |

Run-to-run variance in this run is modest (standard deviations mostly under ~10% of the mean), so gaps of a few percent between Manual, Mapster, and Mapperly sit within the noise and should not be over-interpreted. AutoMapper and Facet, by contrast, are separated from the leaders by margins well outside the error bars.

### Memory Allocation

| Approach   | 1 object | 100 objects | 1,000 objects |
|------------|:--------:|:-----------:|:-------------:|
| Manual     | 184 B    | 5,728 B     | 56,128 B      |
| Mapster    | 112 B    | 5,656 B     | 56,056 B      |
| AutoMapper | 136 B    | 6,992 B     | 64,600 B      |
| Mapperly   | 248 B    | 5,792 B     | 56,192 B      |
| Facet      | 240 B    | 11,328 B    | 112,128 B     |

## Scope And Limits

This benchmark measures warm, in-memory object mapping for one mapping shape: a domain model with a nested address projected to a flat DTO. It does **not** measure startup or configuration cost, `IQueryable` projection, database interaction, or developer productivity.

Configuration for Mapster and AutoMapper is performed during global setup before the benchmarked methods run. The results therefore support conclusions about steady-state mapping throughput and allocation, not first-call latency or application cold-start behaviour.

---

## Analysis

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

All five approaches integrate with the standard `Microsoft.Extensions.DependencyInjection` container. The mechanisms differ:

| Approach | DI Mechanism | Runtime Dependency |
|----------|--------------|--------------------|
| Manual | Register behind a custom interface | None |
| Mapster | First-class `IMapper` interface, mirrors AutoMapper pattern | `Mapster` + `Mapster.DependencyInjection` |
| AutoMapper | First-class `IMapper` via `AddAutoMapper(...)` profile scanning | `AutoMapper` (DI built in since 13.x) |
| Mapperly | Register generated class directly; supports constructor injection of services | None (analyzer only) |
| Facet | No built-in DI — wrap generated constructor behind a custom interface | `Facet` + `Facet.Extensions` |

Mapperly stands out here: the generated mapper class is a standard C# class and can declare constructor dependencies just like any other service, allowing cross-cutting concerns (logging, auditing) to be injected without any special framework support.

---

## Package Support And Maintenance Posture

Long-term package support is a separate decision factor from benchmark performance. A mapper can be fast and still be a poor fit if it depends on a package with irregular releases, limited maintainer activity, uncertain roadmap signals, or weak responsiveness to new .NET/runtime changes.

This matters most for a shared architectural dependency, because a mapping library tends to spread across many services once adopted. If package support slows down, the organisation inherits upgrade friction, compatibility risk, and potentially an emergency migration later.

From that perspective, **manual mapping has the strongest maintenance posture**: it is fully owned internally, has no external package lifecycle risk, and can evolve on the team's schedule. There is no dependency on third-party release cadence, maintainer availability, or community health.

Among external options, package support should be reviewed explicitly before standardising on any one library. Performance alone is not enough; the architecture team should also consider release frequency, maintainer activity, issue responsiveness, .NET version support, and whether the package shows evidence of ongoing investment.

---

## Recommendation

**Manual mapping is the recommended default** for this organisation's production services.

This recommendation is driven less by raw throughput than by engineering posture. In this benchmark, Manual, Mapperly, and Mapster are effectively tied on steady-state performance, so the deciding factors become dependency posture, package maintenance posture, compile-time safety, debugging transparency, and how much indirection the team wants in a core code path.

The performance characteristics of manual mapping are well understood, there are no external dependencies to evaluate for licensing, package support, or supply-chain policy at this layer, and the code is fully visible and debuggable. The mapping logic lives in the codebase and evolves with it.

The traditional objection - the volume of boilerplate required - can be reduced substantially with AI-assisted development tooling, provided the generated mapper is still reviewed and validated like any other handwritten code.

**Mapperly is the recommended alternative** when the team prefers generated mappings, particularly in domains with frequent model changes where compile-time validation of the mapping layer reduces regression risk.

**Mapster** is worth considering when the team wants a mature runtime mapping library and accepts the added runtime configuration layer. Its steady-state edge over manual mapping is marginal and within measurement noise, but it retains the lowest single-object allocation in this run.

**AutoMapper is not recommended based on this benchmark.** It was the slowest mapper measured (up to 2.4x the baseline) and allocates more than the other projection mappers. If it is still being considered for ecosystem or familiarity reasons, licensing and dependency-review questions should be evaluated separately from this performance data.

**Facet** should not be adopted for general projection use cases at this time, given its current alpha status and different design focus.

---

## Summary

| | Manual | Mapster | AutoMapper | Mapperly | Facet |
|---|:---:|:---:|:---:|:---:|:---:|
| Production-ready | ✅ | ✅ | Needs separate review | ✅ | ⚠️ alpha |
| Runtime dependency | None | Yes | Yes | None | Yes |
| Package support risk | None internally | External package review needed | External package review needed | External package review needed | High early-stage risk |
| Startup/config cost measured here | No | No | No | No | No |
| Compile-time validation | — | No | No | ✅ | Partial |
| Steady-state speed (1k) | 1.00x | 0.99x | 1.31x | 1.04x | 2.28x |
| DI integration | Custom interface | First-class `IMapper` | First-class `IMapper` | Constructor injection | Custom interface |
| Recommended for | General use | Teams wanting runtime mapper config | Not recommended from this benchmark | Model-heavy domains | Full-clone DTOs |

Facet is included for completeness, but its benchmarked path is not a like-for-like selective projection comparison with the other four approaches.
