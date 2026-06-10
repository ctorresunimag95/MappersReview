# .NET Object Mapping — Technology Comparison

A proof-of-concept that benchmarks and compares five object-mapping approaches for .NET 10 services, covering steady-state performance, memory allocation, and dependency injection integration.

## Purpose

Help teams make an informed choice between manual mapping and popular mapping libraries by measuring them under identical conditions and summarising the trade-offs beyond raw throughput.

## Candidates

| Approach | Type |
|----------|------|
| **Manual** | Handwritten assignment code |
| **Mapster** | Runtime IL-emit library |
| **AutoMapper** | Runtime expression-compiled library |
| **Mapperly** | Compile-time source generator |
| **Facet** | Compile-time DTO generator |

## Repository Structure

```
MappersComparasion/              # BenchmarkDotNet harness (main entry point)
│  Program.cs                    # Runs benchmarks in Release mode
│  Benchmarks/MapperBenchmarks.cs
│
MappersComparasion.Shared/       # Models, DTOs, and mapper implementations
│  Models/                       # User + Address domain model and UserDto
│  Mappers/                      # Manual, Mapperly, AutoMapper, and Mapster implementations
│
MappersComparasion.DI/           # Standalone demo: DI registration patterns for each approach
│  DI/                           # One demo class per mapper (ManualDIDemo, MapsterDIDemo, …)
│  Program.cs
│
BenchmarkAnalysis.md             # Full benchmark results and architecture recommendation
ManualMappingGuidance.md         # Guidance on generating manual mappers with AI tooling
```

## Running the Benchmarks

```bash
cd MappersComparasion
dotnet run -c Release
```

BenchmarkDotNet artifacts (including the GitHub-formatted results table) are written to `MappersComparasion/BenchmarkDotNet.Artifacts/`.

## Running the DI Demo

```bash
cd MappersComparasion.DI
dotnet run
```

## Key Findings

- **Manual, Mapperly, and Mapster** are statistically tied on steady-state throughput at production scale (100–1,000 objects).
- **AutoMapper** is 1.3–2.4× slower and allocates more than the other projection approaches.
- **Facet** doubles allocation at scale because it generates a full entity copy rather than a selective projection.
- **Mapperly** adds compile-time mapping validation with zero runtime dependency.
- All five approaches integrate with `Microsoft.Extensions.DependencyInjection`.

See [BenchmarkAnalysis.md](BenchmarkAnalysis.md) for the full results table, per-candidate analysis, and recommendation.
