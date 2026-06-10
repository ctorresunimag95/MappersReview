using MappersComparasion.Models;
using Riok.Mapperly.Abstractions;

namespace MappersComparasion.Mappers;

public interface IAuditService
{
    void Log(string message);
}

public class ConsoleAuditService : IAuditService
{
    public void Log(string message) => Console.WriteLine($"  [Audit] {message}");
}

[Mapper]
public partial class MapperlyUserMapperWithService
{
    private readonly IAuditService _auditService;

    public MapperlyUserMapperWithService(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public UserDto MapWithAudit(User src)
    {
        var dto = Map(src);
        _auditService.Log($"Mapped user {src.Id} ({src.FirstName} {src.LastName})");
        return dto;
    }

    [MapProperty("Address.City", nameof(UserDto.City))]
    [MapperIgnoreSource(nameof(User.BirthDate))]
    private partial UserDto Map(User src);
}
