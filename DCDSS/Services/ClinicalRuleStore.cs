using DCDSS.Models.Cds;
using DCDSS.Services.Interfaces;

namespace DCDSS.Services;

public class ClinicalRuleStore : IClinicalRuleStore
{
    private static readonly IList<DiagnosticRule> _diagnosticRules =
    [
        new DiagnosticRule
        {
            Id = "DIA-001",
            Name = "Possible Diabetes Mellitus",
            TriggerSymptoms = ["polyuria", "polydipsia", "weight loss", "fatigue"],
            MinSymptomMatches = 2,
            SuggestedDiagnosis = "Consider screening for Diabetes Mellitus. Recommend fasting glucose and HbA1c.",
            Severity = CdsSeverity.High
        },
        new DiagnosticRule
        {
            Id = "DIA-002",
            Name = "Possible Respiratory Infection",
            TriggerSymptoms = ["fever", "cough", "dyspnea"],
            MinSymptomMatches = 2,
            SuggestedDiagnosis = "Consider respiratory infection (pneumonia/TB). Recommend CXR and sputum culture.",
            Severity = CdsSeverity.Warning
        },
        new DiagnosticRule
        {
            Id = "DIA-003",
            Name = "Possible Acute Coronary Syndrome",
            TriggerSymptoms = ["chest pain", "dyspnea", "diaphoresis"],
            MinSymptomMatches = 2,
            SuggestedDiagnosis = "CRITICAL: Possible Acute Coronary Syndrome. Immediate ECG and troponin.",
            Severity = CdsSeverity.Critical
        }
    ];

    private static readonly IList<DrugInteractionPair> _interactionPairs =
    [
        new DrugInteractionPair
        {
            Id = "INT-001",
            DrugA = "Warfarin",
            DrugB = "Aspirin",
            Severity = CdsSeverity.Critical,
            Description = "Warfarin + Aspirin: significantly increased risk of major bleeding."
        },
        new DrugInteractionPair
        {
            Id = "INT-002",
            DrugA = "Warfarin",
            DrugB = "Ibuprofen",
            Severity = CdsSeverity.Critical,
            Description = "Warfarin + Ibuprofen: significantly increased risk of bleeding and GI haemorrhage."
        },
        new DrugInteractionPair
        {
            Id = "INT-003",
            DrugA = "Enalapril",
            DrugB = "Spironolactone",
            Severity = CdsSeverity.High,
            Description = "Enalapril + Spironolactone: risk of hyperkalaemia. Monitor potassium closely."
        },
        new DrugInteractionPair
        {
            Id = "INT-004",
            DrugA = "Simvastatin",
            DrugB = "Clarithromycin",
            Severity = CdsSeverity.High,
            Description = "Simvastatin + Clarithromycin: risk of myopathy and rhabdomyolysis. Consider alternative."
        }
    ];

    private static readonly IList<AllergyConflict> _allergyConflicts =
    [
        new AllergyConflict
        {
            Id = "ALG-001",
            Allergen = "Penicillin",
            Drugs = ["Penicillin", "Amoxicillin", "Ampicillin", "Amoxiclav", "Flucloxacillin"],
            Severity = CdsSeverity.Critical,
            Description = "Patient has Penicillin allergy. Prescribing a penicillin-class drug is contraindicated."
        },
        new AllergyConflict
        {
            Id = "ALG-002a",
            Allergen = "Sulfa",
            Drugs = ["Sulfamethoxazole", "Co-trimoxazole", "Trimethoprim"],
            Severity = CdsSeverity.Critical,
            Description = "Patient has Sulfa allergy. Prescribing a sulfonamide drug is contraindicated."
        },
        new AllergyConflict
        {
            Id = "ALG-002b",
            Allergen = "Sulfonamide",
            Drugs = ["Sulfamethoxazole", "Co-trimoxazole", "Trimethoprim"],
            Severity = CdsSeverity.Critical,
            Description = "Patient has Sulfonamide allergy. Prescribing a sulfonamide drug is contraindicated."
        },
        new AllergyConflict
        {
            Id = "ALG-003",
            Allergen = "Aspirin",
            Drugs = ["Aspirin"],
            Severity = CdsSeverity.High,
            Description = "Patient has Aspirin allergy. Prescribing Aspirin is not recommended."
        }
    ];

    private static readonly IList<CriticalLabRule> _criticalLabRules =
    [
        new CriticalLabRule
        {
            Id = "LAB-001",
            TestName = "Glucose",
            CriticalLow = 2.5,
            CriticalHigh = 22.0,
            Severity = CdsSeverity.Critical,
            Description = "Critical glucose level detected. Immediate clinical review required."
        },
        new CriticalLabRule
        {
            Id = "LAB-002",
            TestName = "Potassium",
            CriticalLow = 2.5,
            CriticalHigh = 6.5,
            Severity = CdsSeverity.Critical,
            Description = "Critical potassium level detected. Risk of cardiac arrhythmia."
        },
        new CriticalLabRule
        {
            Id = "LAB-003",
            TestName = "Hemoglobin",
            CriticalLow = 6.0,
            CriticalHigh = 20.0,
            Severity = CdsSeverity.Critical,
            Description = "Critical haemoglobin level detected. Consider transfusion or urgent review."
        }
    ];

    private static readonly IList<DrugLabConflictRule> _drugLabConflictRules =
    [
        new DrugLabConflictRule
        {
            Id = "DLC-001",
            Drug = "Metformin",
            LabTest = "Creatinine",
            Operator = ">",
            Threshold = 130.0,
            Severity = CdsSeverity.Critical,
            Description = "Metformin is contraindicated when Creatinine > 130 µmol/L due to risk of lactic acidosis."
        },
        new DrugLabConflictRule
        {
            Id = "DLC-002",
            Drug = "Ibuprofen",
            LabTest = "Creatinine",
            Operator = ">",
            Threshold = 120.0,
            Severity = CdsSeverity.High,
            Description = "Ibuprofen should be avoided when Creatinine > 120 µmol/L due to nephrotoxicity risk."
        },
        new DrugLabConflictRule
        {
            Id = "DLC-003",
            Drug = "Warfarin",
            LabTest = "INR",
            Operator = ">",
            Threshold = 4.0,
            Severity = CdsSeverity.Critical,
            Description = "Warfarin with INR > 4: high bleeding risk. Review dosage immediately."
        }
    ];

    public IList<DiagnosticRule> GetDiagnosticRules() => _diagnosticRules;
    public IList<DrugInteractionPair> GetDrugInteractionPairs() => _interactionPairs;
    public IList<AllergyConflict> GetAllergyConflicts() => _allergyConflicts;
    public IList<CriticalLabRule> GetCriticalLabRules() => _criticalLabRules;
    public IList<DrugLabConflictRule> GetDrugLabConflictRules() => _drugLabConflictRules;
}
