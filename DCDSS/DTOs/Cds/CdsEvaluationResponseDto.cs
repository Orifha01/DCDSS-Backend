namespace DCDSS.DTOs.Cds;

public class CdsEvaluationResponseDto
{
    public IList<CdsFindingDto> DiagnosticRecommendations { get; set; } = [];
    public IList<CdsFindingDto> InteractionWarnings { get; set; } = [];
    public IList<CdsFindingDto> CriticalAlerts { get; set; } = [];
    public DateTime EvaluatedAt { get; set; }
}
