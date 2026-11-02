using DCDSS.DTOs.Common;

namespace DCDSS.DTOs.Medications;

public class MedicationDto : IAuditableResult
{
    public int MedicationId { get; set; }
    public int RecordId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public int? EntityId => MedicationId;
}
