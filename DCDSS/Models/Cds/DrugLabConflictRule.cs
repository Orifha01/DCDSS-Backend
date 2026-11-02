namespace DCDSS.Models.Cds;

public class DrugLabConflictRule
{
    public string Id { get; set; } = string.Empty;
    public string Drug { get; set; } = string.Empty;
    public string LabTest { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public double Threshold { get; set; }
    public CdsSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
}
