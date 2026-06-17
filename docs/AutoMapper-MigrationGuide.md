# AutoMapper → Custom Manual Mapper — Migration Guide

This guide walks through replacing AutoMapper with the custom `IMapper` / `IMapperProfile<,>` library in an existing .NET service. The migration can be done incrementally, one mapping at a time, or in a single pass depending on team preference.

---

## Concept Mapping

| AutoMapper concept | Custom mapper equivalent |
|--------------------|--------------------------|
| `Profile` subclass with `CreateMap<>` | `IMapperProfile<TSource, TDestination>` class |
| `IMapper.Map<TDestination>(source)` | `IMapper.Map<TSource, TDestination>(source)` |
| `services.AddAutoMapper(Assembly)` | `services.AddMappers<TAssemblyMarker>()` |
| Convention-based member matching | Explicit property assignments inside `Map()` |
| `IMappingAction`, `IValueConverter` | Constructor-injected service inside the profile |
| `ProjectTo<>` (IQueryable) | Not supported — use explicit LINQ projections |

---

## Step 1 — Add the CustomMapper Reference

Add a reference to the `CustomMapper` project (or package) in your service's `.csproj` and remove the AutoMapper package references:

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

Each `Profile` class becomes an `IMapperProfile<TSource, TDestination>` class. The explicit assignments that AutoMapper derived from conventions must now be written out.

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

One `CreateMap<A, B>` call in a Profile becomes one `IMapperProfile<A, B>` class. If a Profile contained multiple `CreateMap` calls, split them into one class each.

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

Translate the resolver logic directly into the `Map` method body:

```csharp
// AutoMapper
CreateMap<Order, OrderDto>()
    .ForMember(d => d.DisplayTotal, opt => opt.MapFrom<CurrencyResolver>());

// Custom mapper — inject the dependency instead
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

- [CustomMapper Usage Guide](./CustomMapper-UsageGuide.md)
- [ADR-001: AutoMapper Alternatives](./ADR-001-AutoMapper-Alternatives.md)
- [BenchmarkAnalysis.md](../BenchmarkAnalysis.md)
