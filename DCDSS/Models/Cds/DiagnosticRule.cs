namespace DCDSS.Models.Cds;

public class DiagnosticRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IList<string> TriggerSymptoms { get; set; } = [];
    public int MinSymptomMatches { get; set; }
    public string SuggestedDiagnosis { get; set; } = string.Empty;
    public CdsSeverity Severity { get; set; }
}
