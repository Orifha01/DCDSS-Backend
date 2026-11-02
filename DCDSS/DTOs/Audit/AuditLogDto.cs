namespace DCDSS.DTOs.Audit;

public class AuditLogDto
{
    public int AuditId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}
