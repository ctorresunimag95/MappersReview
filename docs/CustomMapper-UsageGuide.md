# CustomMapper — Usage Guide

This guide explains how to integrate custom mapping into a .NET service using either **CustomSourceGenerator** (recommended, zero handwriting) or **manual mapping profiles** (full control).

## When to Use Each Approach

| Approach | When to Use |
|----------|------------|
| **CustomSourceGenerator** | ✅ Recommended for all new projects; fastest performance at scale (0.77× @ 100 objects), zero handwritten mapping code, source-generated implementations |
| **Manual Mapping** | When CustomSourceGenerator infrastructure is unavailable or for specific custom logic that's hard to express with attributes |

---

## Overview

Both approaches share a common runtime interface:

| Component | Role |
|-----------|------|
| `IMapper` | Single call-site interface: `Map<TSource, TDestination>(source)` |
| `IMapperProfile<TSource, TDestination>` | Contract implemented by each mapping (generated or handwritten) |
| `AddMappers<TAssemblyMarker>()` | DI extension that auto-registers all profiles in an assembly |

### CustomSourceGenerator
Uses the `[Mapper]` attribute on partial classes; implementation is generated at compile time.

### Manual Mapping
Handwritten classes implementing `IMapperProfile<TSource, TDestination>`.

---

## 1. Add the Library Reference

### CustomSourceGenerator (Recommended)

Add a project reference to `CustomMapper.SourceGenerator` in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\CustomMapper.SourceGenerator\CustomMapper.SourceGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
</ItemGroup>
```

### Manual Mapping

Add a project (or package) reference to `CustomMapper` in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\CustomMapper\CustomMapper.csproj" />
</ItemGroup>
```

---

## 2. Register the Mapper in DI

Call `AddMappers<TAssemblyMarker>()` once during service registration, passing any type that lives in the assembly where your profiles are defined.

```csharp
// Program.cs / Startup.cs
builder.Services.AddMappers<Program>();
```

This single call:
- Uses `typeof(TAssemblyMarker).Assembly.GetTypes()` to find every class that implements `IMapperProfile<,>`.
- Registers each profile against its interface with `Scoped` lifetime (configurable).
- Registers `IMapper` itself as `Transient`.

To change the profile lifetime:

```csharp
builder.Services.AddMappers<Program>(ServiceLifetime.Singleton);
```

---

## 3. Define a Mapping Profile

### CustomSourceGenerator (Recommended)

Create one `[Mapper]` partial class per mapping pair with a `partial Map` method. The implementation is generated at compile time.

```csharp
using CustomMapper.SourceGenerator.Runtime;
using MappersComparasion.Models;

[Mapper]
public partial class UserMapper
{
    public partial UserDto Map(User source);

    // Optional: post-generation customization hook
    private void ExtendMap(User source, UserDto destination)
    {
        destination.City = source.Address.City;  // flattening example
    }
}
```

**Rules:**
- One mapper class per source/destination pair.
- Method signature: `public partial TDestination Map(TSource source);`
- The `[Mapper]` attribute signals the generator to produce the implementation.
- Optional `ExtendMap(TSource source, TDestination destination)` hook is called after generated assignments.
- Works with constructor-injected services (see below).

**Performance:** Generated code is identical to handwritten mapping; zero handwriting burden.

### Manual Mapping

Create one class per mapping pair. The class implements `IMapperProfile<TSource, TDestination>` and contains the explicit property assignments.

```csharp
using CustomMapper;

public class UserMapper : IMapperProfile<User, UserDto>
{
    public UserDto Map(User source)
    {
        return new UserDto
        {
            Id        = source.Id,
            FirstName = source.FirstName,
            LastName  = source.LastName,
            City      = source.Address.City
        };
    }
}
```

**Rules:**
- One profile per source/destination pair.
- The class does not need any attributes or base class — implementing the interface is enough.
- Any constructor dependencies (services, configuration) can be injected normally.

### Injecting Services into a Profile

#### CustomSourceGenerator

Because generated mappers are registered as ordinary DI services, constructor injection works without any framework support:

```csharp
[Mapper]
public partial class OrderMapper
{
    private readonly ICurrencyFormatter _formatter;

    public OrderMapper(ICurrencyFormatter formatter)
    {
        _formatter = formatter;
    }

    public partial OrderDto Map(Order source);

    private void ExtendMap(Order source, OrderDto destination)
    {
        destination.Total = _formatter.Format(source.TotalCents);
    }
}
```

#### Manual Mapping

```csharp
public class OrderMapper : IMapperProfile<Order, OrderDto>
{
    private readonly ICurrencyFormatter _formatter;

    public OrderMapper(ICurrencyFormatter formatter)
    {
        _formatter = formatter;
    }

    public OrderDto Map(Order source) => new()
    {
        Id    = source.Id,
        Total = _formatter.Format(source.TotalCents)
    };
}
```

---

## 4. Use IMapper in Application Code

Inject `IMapper` through the constructor and call `Map<TSource, TDestination>`. The call site is identical for both approaches:

```csharp
public class UserService
{
    private readonly IMapper _mapper;
    private readonly IUserRepository _repo;

    public UserService(IMapper mapper, IUserRepository repo)
    {
        _mapper = mapper;
        _repo   = repo;
    }

    public async Task<UserDto> GetUserAsync(int id)
    {
        var user = await _repo.GetByIdAsync(id);
        return _mapper.Map<User, UserDto>(user);  // Works with both approaches
    }
}
```

---

## 5. Mapping Collections

`IMapper` maps one object at a time. Use LINQ to map collections:

```csharp
var dtos = users.Select(u => _mapper.Map<User, UserDto>(u)).ToList();
```

For performance-critical hot paths with large collections, injecting the profile directly via DI avoids the per-item scope creation overhead. Works identically for both approaches:

```csharp
public class UserService
{
    private readonly IMapperProfile<User, UserDto> _userMapper;

    public UserService(IMapperProfile<User, UserDto> userMapper)
    {
        _userMapper = userMapper;
    }

    public IReadOnlyList<UserDto> MapUsers(IEnumerable<User> users)
        => users.Select(_userMapper.Map).ToList();  // Direct profile call, no DI scope per item
}
```

This pattern is particularly valuable for CustomSourceGenerator, which achieves **0.77× performance at 100 objects** — faster than any other option including manual mapping.

---

## 6. Unit Testing a Profile

### CustomSourceGenerator

Generated mappers are plain C# classes and can be instantiated directly without any DI setup:

```csharp
[Fact]
public void UserMapper_MapsAllFields()
{
    var mapper = new UserMapper();  // Generated implementation available at test time
    var user   = new User { Id = 1, FirstName = "Jane", LastName = "Doe",
                             Address = new Address { City = "New York" } };

    var dto = mapper.Map(user);

    Assert.Equal(1,          dto.Id);
    Assert.Equal("Jane",     dto.FirstName);
    Assert.Equal("New York", dto.City);
}
```

### Manual Mapping

Profiles are plain C# classes and can be instantiated directly without any DI setup:

```csharp
[Fact]
public void UserMapper_MapsAllFields()
{
    var mapper = new UserMapper();
    var user   = new User { Id = 1, FirstName = "Jane", LastName = "Doe",
                             Address = new Address { City = "New York" } };

    var dto = mapper.Map(user);

    Assert.Equal(1,          dto.Id);
    Assert.Equal("Jane",     dto.FirstName);
    Assert.Equal("New York", dto.City);
}
```

---

## Quick Reference

### CustomSourceGenerator

```
Assembly contains [Mapper] classes
    [Mapper] partial class UserMapper
         └─ public partial UserDto Map(User source);
         └─ private void ExtendMap(...);  // optional

Build (compile-time)
    CustomSourceGenerator analyzer scans [Mapper] classes
         └─ Generates Map implementation from source
         └─ Optional ExtendMap hook is called after assignments

Startup
    services.AddMappers<Program>()
         └─ Assembly.GetTypes() discovers all generated IMapperProfile<,> implementations
         └─ Registers each as its interface (Scoped)
         └─ Registers IMapper → Mapper (Transient)

Runtime
    IMapper.Map<User, UserDto>(user)
         └─ Creates DI scope
         └─ Resolves IMapperProfile<User, UserDto>
         └─ Calls generated .Map(source)
         └─ Disposes scope, returns result

Performance
    0.77× @ 100 objects (fastest)
    1.01× @ 1,000 objects (tied fastest)
```

### Manual Mapping

```
Assembly contains profile classes
    └─ UserMapper : IMapperProfile<User, UserDto>
    └─ OrderMapper : IMapperProfile<Order, OrderDto>

Startup
    services.AddMappers<Program>()
         └─ Assembly.GetTypes() discovers and registers all IMapperProfile<,> classes
         └─ Registers IMapper → Mapper (Transient)

Runtime
    IMapper.Map<User, UserDto>(user)
         └─ Creates DI scope
         └─ Resolves IMapperProfile<User, UserDto>
         └─ Calls .Map(source)
         └─ Disposes scope, returns result

Performance
    1.02–1.04× baseline (steady, predictable)
```

---

## References

- [ADR-001: AutoMapper Alternatives](./ADR-001-AutoMapper-Alternatives.md) — decision rationale
- [AutoMapper Migration Guide](./AutoMapper-MigrationGuide.md) — how to migrate from AutoMapper
- [BenchmarkAnalysis.md](../BenchmarkAnalysis.md) — performance data for all six approaches
