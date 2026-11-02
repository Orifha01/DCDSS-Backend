using System.ComponentModel.DataAnnotations;

namespace DCDSS.DTOs.Auth;

public class LoginDto
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
