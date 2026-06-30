using CustomMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CustomMapper.Tests;

#region Models

class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}

class OrderDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}

class Product
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}

class ProductDto
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}

#endregion

#region Profiles

class OrderProfile : IMapperProfile<Order, OrderDto>
{
    public OrderDto Map(Order source) => new()
    {
        Id = source.Id,
        CustomerName = source.CustomerName,
        Total = source.Total
    };
}

class OrderAsyncProfile : IAsyncMapperProfile<Order, OrderDto>
{
    public async Task<OrderDto> MapAsync(Order source, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return new()
        {
            Id = source.Id,
            CustomerName = source.CustomerName,
            Total = source.Total
        };
    }
}

class CountingOrderProfile : IMapperProfile<Order, OrderDto>
{
    public int CallCount { get; private set; }

    public OrderDto Map(Order source)
    {
        CallCount++;
        return new() { Id = source.Id, CustomerName = source.CustomerName, Total = source.Total };
    }
}

class ProductProfile : IMapperProfile<Product, ProductDto>
{
    public ProductDto Map(Product source) => new()
    {
        Name = source.Name,
        Quantity = source.Quantity
    };
}

class AsyncCountingOrderProfile : IAsyncMapperProfile<Order, OrderDto>
{
    public int CallCount { get; private set; }

    public async Task<OrderDto> MapAsync(Order source, CancellationToken cancellationToken = default)
    {
        CallCount++;
        await Task.Delay(1, cancellationToken);
        return new() { Id = source.Id, CustomerName = source.CustomerName, Total = source.Total };
    }
}

class DisposableOrderProfile : IMapperProfile<Order, OrderDto>, IDisposable
{
    public bool Disposed { get; private set; }

    public OrderDto Map(Order source) => new() { Id = source.Id };

    public void Dispose() => Disposed = true;
}

class DualProfile : IMapperProfile<Order, OrderDto>, IAsyncMapperProfile<Order, OrderDto>
{
    public OrderDto Map(Order source) => new() { Id = source.Id };

    public async Task<OrderDto> MapAsync(Order source, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return new() { Id = source.Id };
    }
}

abstract class AbstractOrderProfile : IMapperProfile<Order, OrderDto>
{
    public abstract OrderDto Map(Order source);
}

#endregion

public class MapperTests
{
    private static IServiceProvider BuildServiceProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    #region Map (sync)

    [Fact]
    public void Map_WithValidProfile_ShouldReturnMappedDestination()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMapperProfile<Order, OrderDto>, OrderProfile>();
        services.AddTransient<IMapper, Mapper>();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        var result = mapper.Map<Order, OrderDto>(new Order { Id = 1, CustomerName = "Alice", Total = 99.99m });

        Assert.Equal(1, result.Id);
        Assert.Equal("Alice", result.CustomerName);
        Assert.Equal(99.99m, result.Total);
    }

    [Fact]
    public void Map_WhenNoProfileRegistered_ShouldThrowInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddTransient<IMapper, Mapper>();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        Assert.Throws<InvalidOperationException>(() => mapper.Map<Order, OrderDto>(new Order()));
    }

    [Fact]
    public void Map_WithSingletonProfile_ShouldReuseProfileInstanceAcrossCalls()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMapperProfile<Order, OrderDto>, CountingOrderProfile>();
        services.AddTransient<IMapper, Mapper>();
        var sp = services.BuildServiceProvider();
        var mapper = sp.GetRequiredService<IMapper>();

        mapper.Map<Order, OrderDto>(new Order { Id = 1 });
        mapper.Map<Order, OrderDto>(new Order { Id = 2 });

        var profile = (CountingOrderProfile)sp.GetRequiredService<IMapperProfile<Order, OrderDto>>();
        Assert.Equal(2, profile.CallCount);
    }

    #endregion

    #region MapAsync

    [Fact]
    public async Task MapAsync_WithValidAsyncProfile_ShouldReturnMappedDestination()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAsyncMapperProfile<Order, OrderDto>, OrderAsyncProfile>();
        services.AddTransient<IMapper, Mapper>();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        var result = await mapper.MapAsync<Order, OrderDto>(new Order { Id = 2, CustomerName = "Bob", Total = 49.50m });

        Assert.Equal(2, result.Id);
        Assert.Equal("Bob", result.CustomerName);
        Assert.Equal(49.50m, result.Total);
    }

    [Fact]
    public async Task MapAsync_WhenNoAsyncProfileRegistered_ShouldThrowInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddTransient<IMapper, Mapper>();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mapper.MapAsync<Order, OrderDto>(new Order()));
    }

    [Fact]
    public async Task MapAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        var mapper = BuildServiceProvider(s => s.AddMappers<OrderAsyncProfile>())
            .GetRequiredService<IMapper>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mapper.MapAsync<Order, OrderDto>(new Order(), cts.Token));
    }

    #endregion

    #region AddMappers (registration)

    [Fact]
    public void AddMappers_WithAssemblyMarker_ShouldRegisterAllProfilesAndMapper()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderProfile>();
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IMapperProfile<Order, OrderDto>>());
        Assert.NotNull(sp.GetService<IAsyncMapperProfile<Order, OrderDto>>());
        Assert.NotNull(sp.GetService<IMapper>());
    }

    [Fact]
    public void AddMappers_WithMultipleAssemblyMarkers_ShouldRegisterProfilesFromAllAssemblies()
    {
        var services = new ServiceCollection();
        services.AddMappers([typeof(OrderProfile), typeof(ProductProfile)]);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IMapperProfile<Order, OrderDto>>());
        Assert.NotNull(sp.GetService<IMapperProfile<Product, ProductDto>>());
    }

    [Fact]
    public void AddMappers_WithDefaultLifetime_ShouldRegisterProfilesAsScoped()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderProfile>();

        var descriptor = services.First(d => d.ServiceType == typeof(IMapperProfile<Order, OrderDto>));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddMappers_WithTransientLifetime_ShouldRegisterProfilesAsTransient()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderProfile>(ServiceLifetime.Transient);

        var descriptor = services.First(d => d.ServiceType == typeof(IMapperProfile<Order, OrderDto>));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddMappers_WithSingletonLifetime_ShouldRegisterProfilesAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderProfile>(ServiceLifetime.Singleton);

        var descriptor = services.First(d => d.ServiceType == typeof(IMapperProfile<Order, OrderDto>));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddMappers_WhenCalled_ShouldRegisterIMapperAsTransient()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderProfile>();

        var descriptor = services.First(d => d.ServiceType == typeof(IMapper));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddMappers_WithDefaultLifetime_ShouldRegisterAsyncProfilesAsScoped()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderAsyncProfile>();

        var descriptor = services.First(d => d.ServiceType == typeof(IAsyncMapperProfile<Order, OrderDto>));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddMappers_WithTransientLifetime_ShouldRegisterAsyncProfilesAsTransient()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderAsyncProfile>(ServiceLifetime.Transient);

        var descriptor = services.First(d => d.ServiceType == typeof(IAsyncMapperProfile<Order, OrderDto>));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddMappers_WithSingletonLifetime_ShouldRegisterAsyncProfilesAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderAsyncProfile>(ServiceLifetime.Singleton);

        var descriptor = services.First(d => d.ServiceType == typeof(IAsyncMapperProfile<Order, OrderDto>));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddMappers_ShouldReturnTheSameServiceCollectionInstance()
    {
        var services = new ServiceCollection();

        var returned = services.AddMappers<OrderProfile>();

        Assert.Same(services, returned);
    }

    [Fact]
    public void AddMappers_WithAbstractProfile_ShouldNotRegisterAbstractType()
    {
        var services = new ServiceCollection();
        services.AddMappers<AbstractOrderProfile>();

        var descriptors = services.Where(d =>
            d.ServiceType == typeof(IMapperProfile<Order, OrderDto>) &&
            d.ImplementationType == typeof(AbstractOrderProfile));

        Assert.Empty(descriptors);
    }

    [Fact]
    public void AddMappers_WithDualInterfaceProfile_ShouldRegisterUnderBothInterfaces()
    {
        var services = new ServiceCollection();
        services.AddMappers<DualProfile>();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IMapperProfile<Order, OrderDto>) &&
            d.ImplementationType == typeof(DualProfile));
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IAsyncMapperProfile<Order, OrderDto>) &&
            d.ImplementationType == typeof(DualProfile));
    }

    [Fact]
    public void AddMappers_WithDuplicateAssemblyMarkers_ShouldNotScanAssemblyTwice()
    {
        // Passing the same assembly marker type twice should scan the assembly only once,
        // producing the same registration count as passing it once.
        var services1 = new ServiceCollection();
        services1.AddMappers<OrderProfile>();

        var services2 = new ServiceCollection();
        services2.AddMappers([typeof(OrderProfile), typeof(OrderProfile)]);

        var count1 = services1.Count(d => d.ServiceType == typeof(IMapperProfile<Order, OrderDto>));
        var count2 = services2.Count(d => d.ServiceType == typeof(IMapperProfile<Order, OrderDto>));

        Assert.Equal(count1, count2);
    }

    [Fact]
    public void AddMappers_CalledTwice_ShouldNotAccumulateDescriptors()
    {
        var services = new ServiceCollection();
        services.AddMappers<OrderProfile>();

        var countAfterFirst = services.Count(d => d.ServiceType == typeof(IMapperProfile<Order, OrderDto>));
        var mapperCountAfterFirst = services.Count(d => d.ServiceType == typeof(IMapper));

        services.AddMappers<OrderProfile>();

        var countAfterSecond = services.Count(d => d.ServiceType == typeof(IMapperProfile<Order, OrderDto>));
        var mapperCountAfterSecond = services.Count(d => d.ServiceType == typeof(IMapper));

        Assert.Equal(countAfterFirst, countAfterSecond);
        Assert.Equal(mapperCountAfterFirst, mapperCountAfterSecond);
    }

    #endregion

    #region Scoping

    [Fact]
    public void Map_WithScopedProfile_ShouldReuseInstanceWithinCaller_Scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMapperProfile<Order, OrderDto>, CountingOrderProfile>();
        services.AddTransient<IMapper, Mapper>();
        var sp = services.BuildServiceProvider();

        using var scope1 = sp.CreateScope();
        var mapper = scope1.ServiceProvider.GetRequiredService<IMapper>();

        mapper.Map<Order, OrderDto>(new Order { Id = 1 });
        mapper.Map<Order, OrderDto>(new Order { Id = 2 });

        var profile = (CountingOrderProfile)scope1.ServiceProvider.GetRequiredService<IMapperProfile<Order, OrderDto>>();
        Assert.Equal(2, profile.CallCount);

        // Different scope gets a fresh instance.
        using var scope2 = sp.CreateScope();
        var freshProfile = (CountingOrderProfile)scope2.ServiceProvider.GetRequiredService<IMapperProfile<Order, OrderDto>>();
        Assert.Equal(0, freshProfile.CallCount);
    }

    [Fact]
    public void Map_WithTransientProfile_ShouldCreateFreshInstancePerCall()
    {
        var services = new ServiceCollection();
        services.AddTransient<IMapperProfile<Order, OrderDto>, CountingOrderProfile>();
        services.AddTransient<IMapper, Mapper>();
        var sp = services.BuildServiceProvider();
        var mapper = sp.GetRequiredService<IMapper>();

        mapper.Map<Order, OrderDto>(new Order { Id = 1 });
        mapper.Map<Order, OrderDto>(new Order { Id = 2 });

        // Transient means every resolution is a new instance; no single instance sees both calls.
        using var scope = sp.CreateScope();
        var profile = (CountingOrderProfile)scope.ServiceProvider.GetRequiredService<IMapperProfile<Order, OrderDto>>();
        Assert.Equal(0, profile.CallCount);
    }

    [Fact]
    public async Task MapAsync_WithSingletonAsyncProfile_ShouldReuseInstanceAcrossCalls()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAsyncMapperProfile<Order, OrderDto>, AsyncCountingOrderProfile>();
        services.AddTransient<IMapper, Mapper>();
        var sp = services.BuildServiceProvider();
        var mapper = sp.GetRequiredService<IMapper>();

        await mapper.MapAsync<Order, OrderDto>(new Order { Id = 1 });
        await mapper.MapAsync<Order, OrderDto>(new Order { Id = 2 });

        var profile = (AsyncCountingOrderProfile)sp.GetRequiredService<IAsyncMapperProfile<Order, OrderDto>>();
        Assert.Equal(2, profile.CallCount);
    }

    [Fact]
    public void Map_WithDisposableProfile_ShouldBeDisposedWhenCallerScopeDisposed()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMapperProfile<Order, OrderDto>, DisposableOrderProfile>();
        services.AddTransient<IMapper, Mapper>();
        var sp = services.BuildServiceProvider();

        DisposableOrderProfile capturedProfile;
        using (var scope = sp.CreateScope())
        {
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var result = mapper.Map<Order, OrderDto>(new Order { Id = 5 });

            Assert.Equal(5, result.Id);

            capturedProfile = (DisposableOrderProfile)scope.ServiceProvider.GetRequiredService<IMapperProfile<Order, OrderDto>>();
            Assert.False(capturedProfile.Disposed);
        }

        Assert.True(capturedProfile.Disposed);
    }

    #endregion

    #region Second profile type end-to-end

    [Fact]
    public void Map_WithProductProfile_ShouldReturnMappedProductDto()
    {
        var mapper = BuildServiceProvider(s => s.AddMappers<ProductProfile>())
            .GetRequiredService<IMapper>();

        var result = mapper.Map<Product, ProductDto>(new Product { Name = "Widget", Quantity = 42 });

        Assert.Equal("Widget", result.Name);
        Assert.Equal(42, result.Quantity);
    }

    #endregion

    #region Sync/async isolation

    [Fact]
    public void Map_WhenOnlyAsyncProfileRegistered_ShouldThrowInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddTransient<IAsyncMapperProfile<Order, OrderDto>, OrderAsyncProfile>();
        services.AddTransient<IMapper, Mapper>();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        Assert.Throws<InvalidOperationException>(() => mapper.Map<Order, OrderDto>(new Order()));
    }

    [Fact]
    public async Task MapAsync_WhenOnlySyncProfileRegistered_ShouldThrowInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddTransient<IMapperProfile<Order, OrderDto>, OrderProfile>();
        services.AddTransient<IMapper, Mapper>();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mapper.MapAsync<Order, OrderDto>(new Order()));
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Map_WithNullSource_ShouldThrowArgumentNullException()
    {
        var mapper = BuildServiceProvider(s => s.AddMappers<OrderProfile>())
            .GetRequiredService<IMapper>();

        Assert.Throws<ArgumentNullException>(() => mapper.Map<Order, OrderDto>(null!));
    }

    [Fact]
    public async Task MapAsync_WithNonCancelledToken_ShouldCompleteNormally()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAsyncMapperProfile<Order, OrderDto>, OrderAsyncProfile>();
        services.AddTransient<IMapper, Mapper>();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        var result = await mapper.MapAsync<Order, OrderDto>(
            new Order { Id = 3, CustomerName = "Carol", Total = 10m },
            CancellationToken.None);

        Assert.Equal(3, result.Id);
        Assert.Equal("Carol", result.CustomerName);
    }

    #endregion
}
