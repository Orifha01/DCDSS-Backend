using DCDSS.Models.Cds;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class SafetyCheckService : ISafetyCheckService
{
    private readonly IClinicalRuleStore _ruleStore;
    private readonly ILabResultRepository _labResultRepo;
    private readonly IMedicationRepository _medicationRepo;
    private readonly IPatientAllergyRepository _allergyRepo;
    private readonly IReferenceRangeParser _parser;

    public SafetyCheckService(
        IClinicalRuleStore ruleStore,
        ILabResultRepository labResultRepo,
        IMedicationRepository medicationRepo,
        IPatientAllergyRepository allergyRepo,
        IReferenceRangeParser parser)
    {
        _ruleStore = ruleStore;
        _labResultRepo = labResultRepo;
        _medicationRepo = medicationRepo;
        _allergyRepo = allergyRepo;
        _parser = parser;
    }

    public async Task<SafetyCheckResult> CheckPrescriptionAsync(
        int patientId, string drugName, int? excludeMedicationId)
    {
        // Load active medications, excluding the given id if provided
        var activeMeds = await _medicationRepo.GetActiveByPatientAsync(patientId);
        if (excludeMedicationId.HasValue)
            activeMeds = activeMeds.Where(m => m.MedicationId != excludeMedicationId.Value).ToList();

        // Load allergies
        var allergies = await _allergyRepo.GetByPatientAsync(patientId);
        var allergenNames = allergies.Select(a => a.Allergen).ToList();

        // Load latest lab results per test
        var labResults = await _labResultRepo.GetByPatientAsync(patientId);
        var latestLabs = LabResultSelector.LatestPerTest(labResults);

        var conflicts = new List<SafetyConflict>();

        var newDrugTrimmed = drugName.Trim();

        // Drug-drug interactions
        foreach (var med in activeMeds)
        {
            foreach (var pair in _ruleStore.GetDrugInteractionPairs())
            {
                bool match = (pair.DrugA.Trim().Equals(newDrugTrimmed, StringComparison.OrdinalIgnoreCase)
                              && pair.DrugB.Trim().Equals(med.DrugName.Trim(), StringComparison.OrdinalIgnoreCase))
                             || (pair.DrugB.Trim().Equals(newDrugTrimmed, StringComparison.OrdinalIgnoreCase)
                                 && pair.DrugA.Trim().Equals(med.DrugName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match)
                {
                    conflicts.Add(new SafetyConflict
                    {
                        RuleId = pair.Id,
                        Type = CdsFindingType.Interaction,
                        Severity = pair.Severity,
                        Message = pair.Description,
                        RelatedDrug = med.DrugName
                    });
                }
            }
        }

        // Drug-allergy conflicts
        foreach (var allergyConflict in _ruleStore.GetAllergyConflicts())
        {
            bool hasAllergen = allergenNames.Any(a =>
                a.Trim().Equals(allergyConflict.Allergen.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!hasAllergen) continue;

            bool drugMatches = allergyConflict.Drugs.Any(d =>
                d.Trim().Equals(newDrugTrimmed, StringComparison.OrdinalIgnoreCase));

            if (drugMatches)
            {
                conflicts.Add(new SafetyConflict
                {
                    RuleId = allergyConflict.Id,
                    Type = CdsFindingType.Allergy,
                    Severity = allergyConflict.Severity,
                    Message = allergyConflict.Description,
                    RelatedAllergen = allergyConflict.Allergen
                });
            }
        }

        // Drug-lab conflicts
        foreach (var dlcRule in _ruleStore.GetDrugLabConflictRules())
        {
            if (!dlcRule.Drug.Trim().Equals(newDrugTrimmed, StringComparison.OrdinalIgnoreCase))
                continue;

            var lab = latestLabs.FirstOrDefault(l =>
                l.TestName.Contains(dlcRule.LabTest, StringComparison.OrdinalIgnoreCase));

            if (lab == null || lab.ResultValue == null) continue;

            var val = _parser.ParseNumericValue(lab.ResultValue);
            if (val == null) continue;

            bool conditionFired = dlcRule.Operator == "<"
                ? val < dlcRule.Threshold
                : dlcRule.Operator == ">"
                    ? val > dlcRule.Threshold
                    : false;

            if (conditionFired)
            {
                conflicts.Add(new SafetyConflict
                {
                    RuleId = dlcRule.Id,
                    Type = CdsFindingType.DrugLab,
                    Severity = dlcRule.Severity,
                    Message = dlcRule.Description,
                    RelatedDrug = dlcRule.Drug
                });
            }
        }

        bool allowed = !conflicts.Any(c => c.Severity == CdsSeverity.Critical);

        return new SafetyCheckResult { Allowed = allowed, Conflicts = conflicts };
    }
}
