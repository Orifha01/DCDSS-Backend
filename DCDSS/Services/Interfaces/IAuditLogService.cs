namespace DCDSS.Services.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(int? userId, string action, string details);
}
