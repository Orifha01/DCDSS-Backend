namespace DCDSS.Models.Cds;

public class DrugInteractionPair
{
    public string Id { get; set; } = string.Empty;
    public string DrugA { get; set; } = string.Empty;
    public string DrugB { get; set; } = string.Empty;
    public CdsSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
}
