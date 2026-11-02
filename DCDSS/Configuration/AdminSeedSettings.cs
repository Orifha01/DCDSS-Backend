namespace DCDSS.Configuration;

public class AdminSeedSettings
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Department { get; set; }
}
