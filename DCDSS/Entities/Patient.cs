using DCDSS.Entities.Enums;

namespace DCDSS.Entities;

public class Patient
{
    public int PatientId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public BloodType BloodType { get; set; }
    public string? EmergencyContact { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<PatientAllergy> Allergies { get; set; } = new List<PatientAllergy>();
}
