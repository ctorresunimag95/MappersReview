namespace Sample.Services;

public interface IAuditService
{
    void Log(string message);
}

public sealed class ConsoleAuditService : IAuditService
{
    public void Log(string message) => Console.WriteLine($"[audit] {message}");
}
