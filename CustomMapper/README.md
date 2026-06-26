# CustomMapper

A lightweight, DI-based object mapper with support for both sync and async profiles.

---

## 1. Define a profile

```csharp
// Sync
public class UserMapper : IMapperProfile<User, UserDto>
{
    public UserDto Map(User source) => new()
    {
        Id = source.Id,
        Name = source.FirstName
    };
}

// Async (for profiles that need I/O, e.g. DB lookups)
public class UserAsyncMapper : IAsyncMapperProfile<User, UserDto>
{
    public async Task<UserDto> MapAsync(User source, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // simulate async work
        return new() { Id = source.Id, Name = source.FirstName };
    }
}
```

---

## 2. Quick setup (console / test)

```csharp
var userMapper = new UserMapper();
var userAsyncMapper = new UserAsyncMapper();

var user = new User { Id = 1, FirstName = "John" };
var dto = userMapper.Map(user);
var asyncDto = await userAsyncMapper.MapAsync(user);
```

```csharp
var services = new ServiceCollection();
services.AddMappers<Program>(); // scans the assembly of the marker type
var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();
```

Then use it:

```csharp
var dto = mapper.Map<User, UserDto>(user);
var dto = await mapper.MapAsync<User, UserDto>(user);
```

---

## 3. ASP.NET Core / Hosted API

Register once in `Program.cs`:

```csharp
builder.Services.AddMappers<Program>();
```

Inject `IMapper` wherever you need it:

```csharp
public class UserService(IMapper mapper)
{
    public UserDto Get(User user) => mapper.Map<User, UserDto>(user);

    public Task<UserDto> GetAsync(User user, CancellationToken ct)
        => mapper.MapAsync<User, UserDto>(user, ct);
}
```
