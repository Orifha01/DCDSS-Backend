using DCDSS.Data;
using DCDSS.Services.Interfaces;

namespace DCDSS.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly DcdssContext _context;

    public UnitOfWork(DcdssContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
