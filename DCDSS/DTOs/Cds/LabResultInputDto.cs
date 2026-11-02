using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.Cds;

public class LabResultInputDto
{
    [Required]
    public string TestName { get; set; } = string.Empty;

    [Required]
    public string ResultValue { get; set; } = string.Empty;

    [Required]
    public string ReferenceRange { get; set; } = string.Empty;
}
