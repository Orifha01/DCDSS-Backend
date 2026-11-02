using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.MedicalRecords;

public class CreateMedicalRecordDto
{
    [Required]
    public int PatientId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Diagnosis { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? TreatmentNotes { get; set; }

    [Required]
    public DateTime VisitDate { get; set; }
}
