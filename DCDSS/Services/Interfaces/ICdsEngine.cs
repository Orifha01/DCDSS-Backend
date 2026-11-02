using DCDSS.DTOs.Cds;

namespace DCDSS.Services.Interfaces;

public interface ICdsEngine
{
    Task<CdsEvaluationResponseDto> EvaluateAsync(CdsEvaluationRequestDto request);
}
