namespace DCDSS.DTOs.Patients;

public class PatientSummaryDto
{
    public int PatientId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string BloodType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
