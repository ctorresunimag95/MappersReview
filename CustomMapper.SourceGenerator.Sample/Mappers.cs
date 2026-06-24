using CustomMapper.SourceGenerator.Runtime;
using Sample.Models;
using Sample.Services;

namespace Sample.Mappers;

[Mapper]
public partial class CustomerMapper
{
    private readonly IAuditService _auditService;

    public CustomerMapper(IAuditService auditService)
    {
        _auditService = auditService;
    }

    // Implemented by the generator: exact-name/exact-type properties are copied,
    // then ExtendMap is invoked before the destination is returned.
    public partial CustomerDto Map(Customer source);

    private static void ConfigureMapper(MapperConfig config)
    {
        config.Ignore<CustomerDto>(nameof(CustomerDto.DisplayName));
    }

    private void ExtendMap(Customer source, CustomerDto destination)
    {
        _auditService.Log($"Mapped customer {source.Id}");
        destination.DisplayName = $"{source.FirstName} {source.LastName}";
    }
}

[Mapper]
public partial class OrderLineMapper
{
    // Mode (b): demonstrates automatic object-initializer mode for init-only properties.
    public partial OrderLineDto MapOrderLine(OrderLine source);
}

[Mapper]
public partial class AddressMapper
{
    // Mode (c): demonstrates constructor mapping with named arguments.
    [UseConstructor]
    public partial AddressDto MapAddress(Address source);
}