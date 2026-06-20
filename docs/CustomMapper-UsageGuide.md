# CustomMapper — Usage Guide

This guide explains how to integrate the `CustomMapper` library into a .NET service using manual mapping profiles.

---

## Overview

The library provides three things:

| Component | Role |
|-----------|------|
| `IMapper` | Single call-site interface: `Map<TSource, TDestination>(source)` |
| `IMapperProfile<TSource, TDestination>` | Contract implemented by each handwritten mapping class |
| `AddMappers<TAssemblyMarker>()` | DI extension that auto-registers all profiles in an assembly |

---

## 1. Add the Library Reference

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

Rules:
- One profile per source/destination pair.
- The class does not need any attributes or base class — implementing the interface is enough.
- Any constructor dependencies (services, configuration) can be injected normally.

### Injecting Services into a Profile

Because profiles are registered as ordinary DI services, constructor injection works without any framework support:

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

Inject `IMapper` through the constructor and call `Map<TSource, TDestination>`:

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
        return _mapper.Map<User, UserDto>(user);
    }
}
```

---

## 5. Mapping Collections

`IMapper` maps one object at a time. Use LINQ to map collections:

```csharp
var dtos = users.Select(u => _mapper.Map<User, UserDto>(u)).ToList();
```

For performance-critical hot paths with large collections, calling the profile directly via DI avoids the per-item scope creation overhead:

```csharp
public class UserService
{
    private readonly IMapperProfile<User, UserDto> _userMapper;

    public UserService(IMapperProfile<User, UserDto> userMapper)
    {
        _userMapper = userMapper;
    }

    public IReadOnlyList<UserDto> MapUsers(IEnumerable<User> users)
        => users.Select(_userMapper.Map).ToList();
}
```

---

## 6. Unit Testing a Profile

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
```
