using System.ComponentModel.DataAnnotations;
using DCDSS.Entities.Enums;

namespace DCDSS.DTOs.Patients;

public class CreatePatientDto
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Surname { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(Gender))]
    public Gender Gender { get; set; }

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [RegularExpression(@"^[0-9+()\-\s]{7,20}$")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(BloodType))]
    public BloodType BloodType { get; set; }

    [MaxLength(100)]
    public string? EmergencyContact { get; set; }

    public IList<string>? Allergies { get; set; }
}
