using DCDSS.DTOs.Common;

namespace DCDSS.DTOs.Laboratory;

public class LabResultDto : IAuditableResult
{
    public int ResultId { get; set; }
    public int RecordId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? ResultValue { get; set; }
    public string ReferenceRange { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int OrderedBy { get; set; }
    public DateTime DateOrdered { get; set; }

    public int? EntityId => ResultId;
}
