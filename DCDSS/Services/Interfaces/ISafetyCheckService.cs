using DCDSS.Models.Cds;

namespace DCDSS.Services.Interfaces;

public interface ISafetyCheckService
{
    Task<SafetyCheckResult> CheckPrescriptionAsync(int patientId, string drugName, int? excludeMedicationId);
}
