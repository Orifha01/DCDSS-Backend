using DCDSS.Models.Cds;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class CdsRuleEvaluator : ICdsRuleEvaluator
{
    private readonly IClinicalRuleStore _ruleStore;
    private readonly IReferenceRangeParser _parser;

    public CdsRuleEvaluator(IClinicalRuleStore ruleStore, IReferenceRangeParser parser)
    {
        _ruleStore = ruleStore;
        _parser = parser;
    }

    public CdsEvaluationResult Evaluate(CdsEvaluationInput input)
    {
        var result = new CdsEvaluationResult();

        // Normalize inputs
        var normMeds = input.Medications
            .Select(m => m.Trim())
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normAllergies = input.Allergies
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normSymptoms = input.Symptoms
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var latestLabsDict = input.LabResults
            .GroupBy(l => l.TestName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(l => l.ReferenceRange).First(),
                StringComparer.OrdinalIgnoreCase);

        // Diagnostic rules
        foreach (var rule in _ruleStore.GetDiagnosticRules())
        {
            int count = rule.TriggerSymptoms.Count(ts =>
                normSymptoms.Any(s => s.Equals(ts.Trim(), StringComparison.OrdinalIgnoreCase)));

            if (count >= rule.MinSymptomMatches)
            {
                result.DiagnosticRecommendations.Add(new CdsFinding
                {
                    RuleId = rule.Id,
                    Type = CdsFindingType.Diagnostic,
                    Severity = rule.Severity,
                    Title = rule.Name,
                    Description = rule.SuggestedDiagnosis
                });
            }
        }

        // Drug-drug interactions
        foreach (var pair in _ruleStore.GetDrugInteractionPairs())
        {
            bool hasA = normMeds.Any(m => m.Equals(pair.DrugA.Trim(), StringComparison.OrdinalIgnoreCase));
            bool hasB = normMeds.Any(m => m.Equals(pair.DrugB.Trim(), StringComparison.OrdinalIgnoreCase));

            if (hasA && hasB)
            {
                result.InteractionWarnings.Add(new CdsFinding
                {
                    RuleId = pair.Id,
                    Type = CdsFindingType.Interaction,
                    Severity = pair.Severity,
                    Title = $"Drug Interaction: {pair.DrugA} + {pair.DrugB}",
                    Description = pair.Description,
                    RelatedItem = pair.DrugB
                });
            }
        }

        // Allergy conflicts
        foreach (var rule in _ruleStore.GetAllergyConflicts())
        {
            bool hasAllergen = normAllergies.Any(a =>
                a.Equals(rule.Allergen.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!hasAllergen) continue;

            var matchedDrug = rule.Drugs.FirstOrDefault(d =>
                normMeds.Any(m => m.Equals(d.Trim(), StringComparison.OrdinalIgnoreCase)));

            if (matchedDrug != null)
            {
                result.CriticalAlerts.Add(new CdsFinding
                {
                    RuleId = rule.Id,
                    Type = CdsFindingType.Allergy,
                    Severity = rule.Severity,
                    Title = rule.Description,
                    Description = rule.Description,
                    RelatedItem = matchedDrug
                });
            }
        }

        // Critical lab rules
        foreach (var rule in _ruleStore.GetCriticalLabRules())
        {
            foreach (var kvp in latestLabsDict)
            {
                if (!kvp.Key.Contains(rule.TestName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var val = _parser.ParseNumericValue(kvp.Value.ResultValue);
                if (val == null) continue;

                bool isCritical = (rule.CriticalLow.HasValue && val < rule.CriticalLow.Value)
                               || (rule.CriticalHigh.HasValue && val > rule.CriticalHigh.Value);

                if (isCritical)
                {
                    result.CriticalAlerts.Add(new CdsFinding
                    {
                        RuleId = rule.Id,
                        Type = CdsFindingType.CriticalLab,
                        Severity = rule.Severity,
                        Title = $"Critical Lab Value: {kvp.Key}",
                        Description = rule.Description
                    });
                }
            }
        }

        // Drug-lab conflict rules
        foreach (var rule in _ruleStore.GetDrugLabConflictRules())
        {
            bool hasDrug = normMeds.Any(m => m.Equals(rule.Drug.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!hasDrug) continue;

            var labEntry = latestLabsDict.FirstOrDefault(kvp =>
                kvp.Key.Contains(rule.LabTest, StringComparison.OrdinalIgnoreCase));

            if (labEntry.Value == null) continue;

            var val = _parser.ParseNumericValue(labEntry.Value.ResultValue);
            if (val == null) continue;

            bool conditionFired = rule.Operator == "<"
                ? val < rule.Threshold
                : rule.Operator == ">"
                    ? val > rule.Threshold
                    : false;

            if (conditionFired)
            {
                result.InteractionWarnings.Add(new CdsFinding
                {
                    RuleId = rule.Id,
                    Type = CdsFindingType.DrugLab,
                    Severity = rule.Severity,
                    Title = $"{rule.Drug}: {rule.LabTest} conflict",
                    Description = rule.Description,
                    RelatedItem = rule.Drug
                });
            }
        }

        return result;
    }
}
