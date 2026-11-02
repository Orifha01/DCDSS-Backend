using DCDSS.Entities;
using DCDSS.Services.Interfaces;

namespace DCDSS.Repositories.Interfaces;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<(IList<AuditLog> Items, int Total)> GetPagedAsync(int page, int pageSize);
}
