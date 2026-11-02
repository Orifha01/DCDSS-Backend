using DCDSS.DTOs.Common;

namespace DCDSS.DTOs.Auth;

public class UserDto : IAuditableResult
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? EntityId => UserId;
}
