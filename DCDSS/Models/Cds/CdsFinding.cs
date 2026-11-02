namespace DCDSS.Models.Cds;

public class CdsFinding
{
    public string RuleId { get; set; } = string.Empty;
    public CdsFindingType Type { get; set; }
    public CdsSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RelatedItem { get; set; }
}
