using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IDbContextFactory<DcdssContext> _factory;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IDbContextFactory<DcdssContext> factory, ILogger<AuditLogService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task LogAsync(int? userId, string action, string details)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for action {Action}", action);
        }
    }
}
