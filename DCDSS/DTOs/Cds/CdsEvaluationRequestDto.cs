namespace DCDSS.DTOs.Cds;

public class CdsEvaluationRequestDto
{
    public int? PatientId { get; set; }
    public IList<string>? Symptoms { get; set; }
    public IList<LabResultInputDto>? LabResults { get; set; }
    public IList<MedicationInputDto>? Medications { get; set; }
    public IList<string>? Allergies { get; set; }
}
