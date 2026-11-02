using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.Laboratory;

public class UpdateLabResultDto
{
    [Required]
    [MaxLength(50)]
    public string ResultValue { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReferenceRange { get; set; }
}
