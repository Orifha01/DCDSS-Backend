namespace DCDSS.Entities;

public class PatientAllergy
{
    public int PatientAllergyId { get; set; }
    public int PatientId { get; set; }
    public string Allergen { get; set; } = string.Empty;

    public Patient Patient { get; set; } = null!;
}
