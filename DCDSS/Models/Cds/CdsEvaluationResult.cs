namespace DCDSS.Models.Cds;

public class CdsEvaluationResult
{
    public IList<CdsFinding> DiagnosticRecommendations { get; set; } = [];
    public IList<CdsFinding> InteractionWarnings { get; set; } = [];
    public IList<CdsFinding> CriticalAlerts { get; set; } = [];
}
