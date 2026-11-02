using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(DcdssContext context) : base(context) { }

    public Task<User?> GetByUsernameAsync(string username)
    {
        return Query().FirstOrDefaultAsync(u => u.Username == username);
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        return Query().FirstOrDefaultAsync(u => u.Email == email);
    }
}
