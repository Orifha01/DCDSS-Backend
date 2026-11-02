using DCDSS.Data;
using DCDSS.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly DcdssContext _context;

    public Repository(DcdssContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _context.Set<T>().FindAsync(id);
    }

    public async Task<IList<T>> GetAllAsync()
    {
        return await _context.Set<T>().ToListAsync();
    }

    public async Task AddAsync(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
    }

    public void Update(T entity)
    {
        _context.Set<T>().Update(entity);
    }

    public void Remove(T entity)
    {
        _context.Set<T>().Remove(entity);
    }

    public IQueryable<T> Query()
    {
        return _context.Set<T>().AsQueryable();
    }
}
