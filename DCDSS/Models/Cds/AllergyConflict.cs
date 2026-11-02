namespace DCDSS.Models.Cds;

public class AllergyConflict
{
    public string Id { get; set; } = string.Empty;
    public string Allergen { get; set; } = string.Empty;
    public IList<string> Drugs { get; set; } = [];
    public CdsSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
}
