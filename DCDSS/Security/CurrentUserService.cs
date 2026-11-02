using System.Security.Claims;
using DCDSS.Services.Interfaces;

namespace DCDSS.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public int? GetUserId()
    {
        var value = _accessor.HttpContext?.User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }

    public string? GetRole() => _accessor.HttpContext?.User.FindFirst("role")?.Value;

    public string? GetUsername() => _accessor.HttpContext?.User.FindFirst("name")?.Value;
}
