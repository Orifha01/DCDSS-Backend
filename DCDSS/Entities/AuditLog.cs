namespace DCDSS.Entities;

public class AuditLog
{
    public int AuditId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }

    public User? User { get; set; }
}
