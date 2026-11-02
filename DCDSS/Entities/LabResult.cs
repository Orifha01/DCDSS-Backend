using DCDSS.Entities.Enums;

namespace DCDSS.Entities;

public class LabResult
{
    public int ResultId { get; set; }
    public int RecordId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? ResultValue { get; set; }
    public string ReferenceRange { get; set; } = string.Empty;
    public LabResultStatus Status { get; set; }
    public int OrderedBy { get; set; }
    public DateTime DateOrdered { get; set; }

    public MedicalRecord Record { get; set; } = null!;
    public User OrderedByUser { get; set; } = null!;
}
