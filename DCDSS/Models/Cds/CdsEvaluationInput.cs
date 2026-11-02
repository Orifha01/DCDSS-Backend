namespace DCDSS.Models.Cds;

public class CdsEvaluationInput
{
    public IList<string> Symptoms { get; set; } = [];
    public IList<LabResultInput> LabResults { get; set; } = [];
    public IList<string> Medications { get; set; } = [];
    public IList<string> Allergies { get; set; } = [];
}
