
using CustomMapper.SourceGenerator;
using CustomMapper.SourceGenerator.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Sample.Models;
using Sample.Services;

var customer = new Customer
{
    Id = 42,
    FirstName = "Ada",
    LastName = "Lovelace",
    Email = "ada@example.com",
};


var services = new ServiceCollection();
services.AddSingleton<IAuditService, ConsoleAuditService>();
services.AddGeneratedMappers();

using (var provider = services.BuildServiceProvider())
using (var scope = provider.CreateScope())
{
    var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
    var dto = mapper.Map<Customer, CustomerDto>(customer);

    Console.WriteLine($"Mapped: {dto}");
    Require(dto.Id == customer.Id, "Id copied");
    Require(dto.FirstName == customer.FirstName, "FirstName copied");
    Require(dto.LastName == customer.LastName, "LastName copied");
    Require(dto.Email == customer.Email, "Email copied");

    // DisplayName proves ExtendMap ran with an injected service available.
    Require(dto.DisplayName == "Ada Lovelace", "DisplayName set by ExtendMap");

    Console.WriteLine("Mapping and ExtendMap logic executed correctly.\n");

    // Unregistered pair must throw InvalidOperationException.
    try
    {
        mapper.Map<Customer, Customer>(customer);
        Require(false, "unregistered pair should have thrown");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("Unregistered pair threw InvalidOperationException as expected.");
    }
}

// --- 2. Lifetime override: Transient yields distinct per-pair mapper instances. ---
var transientServices = new ServiceCollection();
transientServices.AddSingleton<IAuditService, ConsoleAuditService>();
transientServices.AddGeneratedMappers(ServiceLifetime.Transient);

using (var provider = transientServices.BuildServiceProvider())
{
    var a = provider.GetRequiredService<IMapper<Customer, CustomerDto>>();
    var b = provider.GetRequiredService<IMapper<Customer, CustomerDto>>();
    Require(!ReferenceEquals(a, b), "Transient lifetime yields distinct instances");
}

Console.WriteLine("All checks passed.");

static void Require(bool condition, string label)
{
    if (!condition)
    {
        Console.Error.WriteLine($"FAILED: {label}");
        throw new InvalidOperationException($"Validation failed: {label}");
    }
    Console.WriteLine($"OK: {label}");
}
