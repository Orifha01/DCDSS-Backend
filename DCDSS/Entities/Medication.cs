using DCDSS.Entities.Enums;

namespace DCDSS.Entities;

public class Medication
{
    public int MedicationId { get; set; }
    public int RecordId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public MedicationStatus Status { get; set; }

    public MedicalRecord Record { get; set; } = null!;
}
