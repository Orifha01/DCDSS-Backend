using DCDSS.Entities;
using DCDSS.Services.Interfaces;

namespace DCDSS.Repositories.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
}
