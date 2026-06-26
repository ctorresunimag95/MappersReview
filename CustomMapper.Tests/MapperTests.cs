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
        var mapper = BuildServiceProvider(s => s.AddMappers<OrderProfile>())
            .GetRequiredService<IMapper>();

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
        services.AddMappers<CountingOrderProfile>(ServiceLifetime.Singleton);
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
        var mapper = BuildServiceProvider(s => s.AddMappers<OrderAsyncProfile>())
            .GetRequiredService<IMapper>();

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

    #endregion
}
