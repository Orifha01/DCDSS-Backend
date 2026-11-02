using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.Laboratory;

public class OrderLabTestDto
{
    [Required]
    public int RecordId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TestName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ReferenceRange { get; set; } = string.Empty;
}
