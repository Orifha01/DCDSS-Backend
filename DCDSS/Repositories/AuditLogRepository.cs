using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(DcdssContext context) : base(context) { }

    public async Task<(IList<AuditLog> Items, int Total)> GetPagedAsync(int page, int pageSize)
    {
        var q = Query()
            .Include(al => al.User)
            .OrderByDescending(al => al.Timestamp);

        int total = await q.CountAsync();
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
