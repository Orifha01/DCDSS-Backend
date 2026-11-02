using DCDSS.DTOs.Common;
using DCDSS.DTOs.Laboratory;
using DCDSS.DTOs.Medications;

namespace DCDSS.DTOs.MedicalRecords;

public class MedicalRecordDto : IAuditableResult
{
    public int RecordId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? TreatmentNotes { get; set; }
    public DateTime VisitDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public IList<LabResultDto> LabResults { get; set; } = [];
    public IList<MedicationDto> Medications { get; set; } = [];

    public int? EntityId => RecordId;
}
