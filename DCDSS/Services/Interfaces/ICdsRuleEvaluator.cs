using DCDSS.Models.Cds;

namespace DCDSS.Services.Interfaces;

public interface ICdsRuleEvaluator
{
    CdsEvaluationResult Evaluate(CdsEvaluationInput input);
}
