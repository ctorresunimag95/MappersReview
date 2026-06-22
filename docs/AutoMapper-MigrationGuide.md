# AutoMapper Migration Guide

This guide walks through replacing AutoMapper with either **CustomSourceGenerator** (recommended) or the custom `IMapper` / `IMapperProfile<,>` manual mapper library in an existing .NET service.

**Recommendation:** Use **CustomSourceGenerator** for new migrations. It provides source-generated mapping code with zero handwriting burden and best-in-class performance (0.77× at 100 objects, 1.01× at 1,000 objects). Manual `IMapperProfile<,>` mapping remains viable for teams without source generator infrastructure.

The migration can be done incrementally, one mapping at a time, or in a single pass depending on team preference.

---

## Concept Mapping

| AutoMapper concept | CustomSourceGenerator | Manual IMapperProfile |
|--------------------|----------------------|----------------------|
| `Profile` with `CreateMap<>` | `[Mapper] partial class` with partial `Map` method | `IMapperProfile<TSource, TDestination>` class |
| `IMapper.Map<TDestination>(source)` | `IMapper.Map<TSource, TDestination>(source)` (same) | `IMapper.Map<TSource, TDestination>(source)` (same) |
| `services.AddAutoMapper(Assembly)` | `services.AddMappers<TAssemblyMarker>()` | `services.AddMappers<TAssemblyMarker>()` |
| Convention-based member matching | Generated property assignments (compile-time) | Explicit property assignments (handwritten) |
| `IMappingAction`, `IValueConverter` | Constructor-injected service + `ExtendMap` hook | Constructor-injected service inside profile |
| `ProjectTo<>` (IQueryable) | Not supported — use explicit LINQ projections | Not supported — use explicit LINQ projections |

---

## Step 1 — Add the Mapper Reference

### Option A: CustomSourceGenerator (Recommended)

Add a reference to the `CustomMapper.SourceGenerator` project in your service's `.csproj` and remove the AutoMapper package references:

```xml
<!-- Remove -->
<PackageReference Include="AutoMapper" Version="*" />
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="*" />

<!-- Add -->
<ProjectReference Include="..\CustomMapper.SourceGenerator\CustomMapper.SourceGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
```

The source generator runs at compile time; no runtime library reference is needed.

### Option B: Manual Mapping

Alternatively, add a reference to the `CustomMapper` manual mapping library:

```xml
<!-- Remove -->
<PackageReference Include="AutoMapper" Version="*" />
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="*" />

<!-- Add -->
<ProjectReference Include="..\CustomMapper\CustomMapper.csproj" />

<!-- Or install NuGet package -->
<PackageReference Include="CustomMapper" Version="*" />
```

---

## Step 2 — Replace DI Registration

**Before (AutoMapper):**

```csharp
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

**After (Custom mapper):**

```csharp
builder.Services.AddMappers<Program>();
```

Both calls scan the assembly for mapping definitions. No other startup change is required.

---

## Step 3 — Convert AutoMapper Profiles

### Option A: CustomSourceGenerator (Recommended)

Each `Profile` class becomes a `[Mapper] partial class` with a partial `Map` method signature. The implementation is generated at compile time.

**Before:**

```csharp
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.City, opt => opt.MapFrom(s => s.Address.City));
    }
}
```

**After:**

```csharp
using CustomMapper.SourceGenerator.Runtime;

[Mapper]
public partial class UserMapper
{
    [MapProperty(nameof(User.Address) + "." + nameof(Address.City), nameof(UserDto.City))]
    public partial UserDto Map(User source);

    // Optional: post-generation customization
    private void ExtendMap(User source, UserDto destination)
    {
        destination.City = source.Address.City;
    }
}
```

One `CreateMap<A, B>` call becomes one `[Mapper]` partial class. The method signature is declared as `partial`; the generator produces the implementation at compile time. The optional `ExtendMap` hook is called after generated assignments for any custom logic.

### Option B: Manual Mapping

Each `Profile` class becomes an `IMapperProfile<TSource, TDestination>` class with explicit property assignments.

**Before:**

```csharp
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.City, opt => opt.MapFrom(s => s.Address.City));
    }
}
```

**After:**

```csharp
using CustomMapper;

public class UserMapper : IMapperProfile<User, UserDto>
{
    public UserDto Map(User source) => new()
    {
        Id        = source.Id,
        FirstName = source.FirstName,
        LastName  = source.LastName,
        City      = source.Address.City
    };
}
```

One `CreateMap<A, B>` call becomes one `IMapperProfile<A, B>` class. If a Profile contained multiple `CreateMap` calls, split them into one class each.

---

## Step 4 — Replace Call Sites

The call-site signature is nearly identical. The only difference is that the source type must be provided explicitly (it is inferred from the argument in AutoMapper but required here).

**Before:**

```csharp
var dto = _mapper.Map<UserDto>(user);
```

**After:**

```csharp
var dto = _mapper.Map<User, UserDto>(user);
```

For collection mapping, replace `_mapper.Map<IEnumerable<UserDto>>(users)` with a LINQ select:

**Before:**

```csharp
var dtos = _mapper.Map<IEnumerable<UserDto>>(users);
```

**After:**

```csharp
var dtos = users.Select(u => _mapper.Map<User, UserDto>(u)).ToList();
```

This pattern is appropriate for ordinary application flows and small collections. For hot paths or large collections, prefer injecting `IMapperProfile<User, UserDto>` directly and mapping with the profile to avoid creating a DI scope per item.

---

## Step 5 — Handle Advanced AutoMapper Features

### ForMember / Custom resolvers

**CustomSourceGenerator:**

Inject the dependency and use `ExtendMap` to apply custom logic:

```csharp
// AutoMapper
CreateMap<Order, OrderDto>()
    .ForMember(d => d.DisplayTotal, opt => opt.MapFrom<CurrencyResolver>());

// CustomSourceGenerator
[Mapper]
public partial class OrderMapper
{
    private readonly ICurrencyFormatter _formatter;
    public OrderMapper(ICurrencyFormatter formatter) => _formatter = formatter;

    public partial OrderDto Map(Order source);

    private void ExtendMap(Order source, OrderDto destination)
    {
        destination.DisplayTotal = _formatter.Format(source.TotalCents);
    }
}
```

**Manual Mapping:**

Translate the resolver logic directly into the `Map` method body:

```csharp
// AutoMapper
CreateMap<Order, OrderDto>()
    .ForMember(d => d.DisplayTotal, opt => opt.MapFrom<CurrencyResolver>());

// Manual IMapperProfile
public class OrderMapper : IMapperProfile<Order, OrderDto>
{
    private readonly ICurrencyFormatter _formatter;
    public OrderMapper(ICurrencyFormatter formatter) => _formatter = formatter;

    public OrderDto Map(Order source) => new()
    {
        Id           = source.Id,
        DisplayTotal = _formatter.Format(source.TotalCents)
    };
}
```

### BeforeMap / AfterMap

Move any pre/post logic into the `Map` method directly:

```csharp
public UserDto Map(User source)
{
    // pre-map logic here
    var dto = new UserDto { ... };
    // post-map logic here
    return dto;
}
```

### Conditional mapping (`Condition`, `PreCondition`)

Use a standard `if` expression or conditional operator:

```csharp
MiddleName = string.IsNullOrEmpty(source.MiddleName) ? null : source.MiddleName
```

### Ignore (`Ignore()`)

Simply omit the property from the `Map` method — unassigned properties keep their default value.

### Nested object mapping

Call the nested profile directly or inject it:

```csharp
public class OrderMapper : IMapperProfile<Order, OrderDto>
{
    private readonly IMapperProfile<Address, AddressDto> _addressMapper;

    public OrderMapper(IMapperProfile<Address, AddressDto> addressMapper)
        => _addressMapper = addressMapper;

    public OrderDto Map(Order source) => new()
    {
        Id      = source.Id,
        Address = _addressMapper.Map(source.ShippingAddress)
    };
}
```

### ReverseMap

Create a second `IMapperProfile` class for the reverse direction:

```csharp
public class UserDtoToUserMapper : IMapperProfile<UserDto, User>
{
    public User Map(UserDto source) => new() { ... };
}
```

### `ProjectTo<>` (IQueryable projections)

`ProjectTo<>` is not supported by this library — it is an AutoMapper-specific feature that translates mappings into LINQ expression trees for server-side SQL projection. Replace these with explicit `Select` expressions targeting the ORM query directly:

```csharp
// AutoMapper
var dtos = await context.Users.ProjectTo<UserDto>(_mapper.ConfigurationProvider).ToListAsync();

// Custom mapper — explicit LINQ projection
var dtos = await context.Users
    .Select(u => new UserDto
    {
        Id        = u.Id,
        FirstName = u.FirstName,
        LastName  = u.LastName,
        City      = u.Address.City
    })
    .ToListAsync();
```

---

## Step 6 — Remove AutoMapper Namespaces

Once all profiles and call sites are converted, remove any remaining `using AutoMapper;` statements and delete the AutoMapper `Profile` classes.

---

## Migration Checklist

### CustomSourceGenerator Path
- [ ] Add `CustomMapper.SourceGenerator` project reference; remove AutoMapper packages.
- [ ] Replace `AddAutoMapper(...)` with `AddMappers<TAssemblyMarker>()`.
- [ ] Convert each `Profile` class to a `[Mapper] partial class` with partial `Map` method.
- [ ] Update call sites from `Map<TDest>(source)` to `Map<TSource, TDest>(source)`.
- [ ] Replace collection mappings with LINQ `.Select(...)`.
- [ ] Translate any `ProjectTo<>` usages to explicit LINQ projections.
- [ ] Add `ExtendMap` hooks for any custom resolver/post-processing logic.
- [ ] Delete AutoMapper `Profile` classes and remove `using AutoMapper;`.
- [ ] Build to trigger source generation.
- [ ] Run the full test suite to verify correctness.

### Manual Mapping Path
- [ ] Add `CustomMapper` reference; remove AutoMapper packages.
- [ ] Replace `AddAutoMapper(...)` with `AddMappers<TAssemblyMarker>()`.
- [ ] Convert each `Profile` class to one or more `IMapperProfile<,>` classes.
- [ ] Update call sites from `Map<TDest>(source)` to `Map<TSource, TDest>(source)`.
- [ ] Replace collection mappings with LINQ `.Select(...)`.
- [ ] Translate any `ProjectTo<>` usages to explicit LINQ projections.
- [ ] Delete AutoMapper `Profile` classes and remove `using AutoMapper;`.
- [ ] Run the full test suite to verify correctness.

---

## Reference

- [CustomSourceGenerator Usage](../CustomMapper.SourceGenerator/) — in-house source generator approach (recommended)
- [CustomMapper Manual Mapping Guide](./CustomMapper-UsageGuide.md) — manual `IMapperProfile<,>` approach
- [ADR-001: AutoMapper Alternatives](./ADR-001-AutoMapper-Alternatives.md) — architecture decision and rationale
- [BenchmarkAnalysis.md](../BenchmarkAnalysis.md) — performance comparison of all approaches
