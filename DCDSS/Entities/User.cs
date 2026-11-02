using DCDSS.Entities.Enums;

namespace DCDSS.Entities;

public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Department { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<MedicalRecord> AuthoredMedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<LabResult> OrderedLabResults { get; set; } = new List<LabResult>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
