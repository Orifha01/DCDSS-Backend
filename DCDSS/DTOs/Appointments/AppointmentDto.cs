using DCDSS.DTOs.Common;

namespace DCDSS.DTOs.Appointments;

public class AppointmentDto : IAuditableResult
{
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PatientName { get; set; }
    public string? DoctorName { get; set; }

    public int? EntityId => AppointmentId;
}
