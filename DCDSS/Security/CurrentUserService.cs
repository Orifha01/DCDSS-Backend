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
        var user = _accessor.HttpContext?.User;
        if (user == null) return null;

        // Try short claim name first (when MapInboundClaims = false)
        var value = user.FindFirst("sub")?.Value
                 ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(value, out var id) ? id : null;
    }

    public string? GetRole() =>
        _accessor.HttpContext?.User.FindFirst("role")?.Value
        ?? _accessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;

    public string? GetUsername() =>
        _accessor.HttpContext?.User.FindFirst("name")?.Value
        ?? _accessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
}
