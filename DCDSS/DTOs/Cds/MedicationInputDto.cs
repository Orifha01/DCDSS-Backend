using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.Cds;

public class MedicationInputDto
{
    [Required]
    public string DrugName { get; set; } = string.Empty;

    public string? Dosage { get; set; }
}
