using DCDSS.DTOs.Common;
using DCDSS.DTOs.MedicalRecords;

namespace DCDSS.DTOs.Patients;

public class PatientDetailDto : IAuditableResult
{
    public int PatientId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string BloodType { get; set; } = string.Empty;
    public string? EmergencyContact { get; set; }
    public DateTime CreatedAt { get; set; }
    public IList<string> Allergies { get; set; } = [];
    public IList<MedicalRecordDto> MedicalHistory { get; set; } = [];

    public int? EntityId => PatientId;
}
