using DCDSS.Entities.Enums;

namespace DCDSS.Entities;

public class MedicalRecord
{
    public int RecordId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? TreatmentNotes { get; set; }
    public DateTime VisitDate { get; set; }
    public MedicalRecordStatus Status { get; set; }

    public Patient Patient { get; set; } = null!;
    public User Doctor { get; set; } = null!;
    public ICollection<LabResult> LabResults { get; set; } = new List<LabResult>();
    public ICollection<Medication> Medications { get; set; } = new List<Medication>();
}
