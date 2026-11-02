using System.Security.Cryptography;
using DCDSS.Configuration;
using DCDSS.Entities;
using DCDSS.Entities.Enums;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DCDSS.Services;

public class AdminSeeder : IAdminSeeder
{
    private readonly IUserRepository _userRepo;
    private readonly PasswordHasher<User> _hasher;
    private readonly AdminSeedSettings _settings;
    private readonly ILogger<AdminSeeder> _logger;
    private readonly IUnitOfWork _uow;

    public AdminSeeder(
        IUserRepository userRepo,
        PasswordHasher<User> hasher,
        IOptions<AdminSeedSettings> settings,
        ILogger<AdminSeeder> logger,
        IUnitOfWork uow)
    {
        _userRepo = userRepo;
        _hasher = hasher;
        _settings = settings.Value;
        _logger = logger;
        _uow = uow;
    }

    public async Task SeedAsync()
    {
        bool adminExists = await _userRepo.Query()
            .AnyAsync(u => u.Role == UserRole.Administrator);

        if (adminExists) return;

        string plainPassword;

        if (string.IsNullOrWhiteSpace(_settings.Password))
        {
            plainPassword = RandomNumberGenerator.GetString(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789",
                16);
            _logger.LogWarning(
                "Seeded initial administrator '{Username}' with generated password '{Password}'. Rotate it immediately.",
                _settings.Username,
                plainPassword);
        }
        else
        {
            plainPassword = _settings.Password;
        }

        var user = new User
        {
            Username = _settings.Username,
            FullName = _settings.FullName,
            Email = _settings.Email,
            Role = UserRole.Administrator,
            Department = _settings.Department,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _hasher.HashPassword(user, plainPassword);

        await _userRepo.AddAsync(user);
        await _uow.SaveChangesAsync();
    }
}
