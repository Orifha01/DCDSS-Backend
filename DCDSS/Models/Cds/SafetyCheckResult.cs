namespace DCDSS.Models.Cds;

public class SafetyCheckResult
{
    public bool Allowed { get; set; }
    public IList<SafetyConflict> Conflicts { get; set; } = [];
}
