namespace DCDSS.DTOs.Medications;

public class SafetyWarningDto
{
    public string RuleId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedDrug { get; set; }
    public string? RelatedAllergen { get; set; }
}
