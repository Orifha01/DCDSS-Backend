namespace DCDSS.DTOs.Cds;

public class CdsFindingDto
{
    public string RuleId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RelatedItem { get; set; }
}
