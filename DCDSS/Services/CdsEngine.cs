using DCDSS.DTOs.Cds;
using DCDSS.Models.Cds;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class CdsEngine : ICdsEngine
{
    private readonly IPatientAllergyRepository _allergyRepo;
    private readonly IMedicationRepository _medicationRepo;
    private readonly ILabResultRepository _labResultRepo;
    private readonly ICdsRuleEvaluator _evaluator;

    public CdsEngine(
        IPatientAllergyRepository allergyRepo,
        IMedicationRepository medicationRepo,
        ILabResultRepository labResultRepo,
        ICdsRuleEvaluator evaluator)
    {
        _allergyRepo = allergyRepo;
        _medicationRepo = medicationRepo;
        _labResultRepo = labResultRepo;
        _evaluator = evaluator;
    }

    public async Task<CdsEvaluationResponseDto> EvaluateAsync(CdsEvaluationRequestDto request)
    {
        // Start with stored patient data if PatientId provided
        var labDict = new Dictionary<string, LabResultInput>(StringComparer.OrdinalIgnoreCase);
        var storedMedNames = new List<string>();
        var storedAllergens = new List<string>();

        if (request.PatientId.HasValue)
        {
            int patientId = request.PatientId.Value;

            var storedAllergies = await _allergyRepo.GetByPatientAsync(patientId);
            storedAllergens = storedAllergies.Select(a => a.Allergen).ToList();

            var storedMeds = await _medicationRepo.GetActiveByPatientAsync(patientId);
            storedMedNames = storedMeds.Select(m => m.DrugName).ToList();

            var storedLabs = await _labResultRepo.GetByPatientAsync(patientId);
            var latestStoredLabs = LabResultSelector.LatestPerTest(storedLabs);

            foreach (var lab in latestStoredLabs)
            {
                labDict[lab.TestName] = new LabResultInput
                {
                    TestName = lab.TestName,
                    ResultValue = lab.ResultValue ?? string.Empty,
                    ReferenceRange = lab.ReferenceRange
                };
            }
        }

        // Overlay explicit lab results (explicit wins)
        if (request.LabResults != null)
        {
            foreach (var dto in request.LabResults)
            {
                labDict[dto.TestName] = new LabResultInput
                {
                    TestName = dto.TestName,
                    ResultValue = dto.ResultValue,
                    ReferenceRange = dto.ReferenceRange
                };
            }
        }

        // Merge medications: stored + explicit, dedup
        var explicitMedNames = request.Medications?.Select(m => m.DrugName) ?? Enumerable.Empty<string>();
        var mergedMeds = storedMedNames
            .Concat(explicitMedNames)
            .Select(m => m.Trim())
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Merge allergies: stored + explicit, dedup
        var explicitAllergies = request.Allergies ?? [];
        var mergedAllergies = storedAllergens
            .Concat(explicitAllergies)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Symptoms: explicit only, dedup
        var mergedSymptoms = (request.Symptoms ?? [])
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var evalInput = new CdsEvaluationInput
        {
            Symptoms = mergedSymptoms,
            LabResults = labDict.Values.ToList(),
            Medications = mergedMeds,
            Allergies = mergedAllergies
        };

        var evalResult = _evaluator.Evaluate(evalInput);

        return new CdsEvaluationResponseDto
        {
            DiagnosticRecommendations = evalResult.DiagnosticRecommendations
                .Select(MapFinding).ToList(),
            InteractionWarnings = evalResult.InteractionWarnings
                .Select(MapFinding).ToList(),
            CriticalAlerts = evalResult.CriticalAlerts
                .Select(MapFinding).ToList(),
            EvaluatedAt = DateTime.UtcNow
        };
    }

    private static CdsFindingDto MapFinding(CdsFinding f) => new()
    {
        RuleId = f.RuleId,
        Type = f.Type.ToString(),
        Severity = f.Severity.ToString(),
        Title = f.Title,
        Description = f.Description,
        RelatedItem = f.RelatedItem
    };
}
