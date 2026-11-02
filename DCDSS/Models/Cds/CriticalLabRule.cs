namespace DCDSS.Models.Cds;

public class CriticalLabRule
{
    public string Id { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public double? CriticalLow { get; set; }
    public double? CriticalHigh { get; set; }
    public CdsSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
}
