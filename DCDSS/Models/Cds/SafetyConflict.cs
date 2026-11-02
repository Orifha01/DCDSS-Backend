namespace DCDSS.Models.Cds;

public class SafetyConflict
{
    public string RuleId { get; set; } = string.Empty;
    public CdsFindingType Type { get; set; }
    public CdsSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RelatedDrug { get; set; }
    public string? RelatedAllergen { get; set; }
}
