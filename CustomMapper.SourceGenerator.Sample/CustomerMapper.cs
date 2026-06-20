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

    private void ExtendMap(Customer source, CustomerDto destination)
    {
        _auditService.Log($"Mapped customer {source.Id}");
        destination.DisplayName = $"{source.FirstName} {source.LastName}";
    }
}
