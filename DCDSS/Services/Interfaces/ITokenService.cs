using DCDSS.Entities;

namespace DCDSS.Services.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}
