using DCDSS.Models.Cds;

namespace DCDSS.Services.Interfaces;

public interface IClinicalRuleStore
{
    IList<DiagnosticRule> GetDiagnosticRules();
    IList<DrugInteractionPair> GetDrugInteractionPairs();
    IList<AllergyConflict> GetAllergyConflicts();
    IList<CriticalLabRule> GetCriticalLabRules();
    IList<DrugLabConflictRule> GetDrugLabConflictRules();
}
