using DCDSS.DTOs.Common;

namespace DCDSS.DTOs.Medications;

public class PrescribeMedicationResponseDto : IAuditableResult
{
    public MedicationDto Medication { get; set; } = null!;
    public IList<SafetyWarningDto> Warnings { get; set; } = [];

    public int? EntityId => Medication?.MedicationId;
}
