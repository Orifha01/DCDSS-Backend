using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.Medications;

public class CreateMedicationDto
{
    [Required]
    public int RecordId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DrugName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Dosage { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Frequency { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
}
