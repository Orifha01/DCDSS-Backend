using DCDSS.Configuration;
using DCDSS.DTOs.Auth;
using DCDSS.Entities;
using DCDSS.Exceptions;
using DCDSS.Repositories.Interfaces;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DCDSS.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly PasswordHasher<User> _hasher;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserRepository userRepo,
        IUnitOfWork uow,
        PasswordHasher<User> hasher,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings)
    {
        _userRepo = userRepo;
        _uow = uow;
        _hasher = hasher;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        var existingUsername = await _userRepo.GetByUsernameAsync(dto.Username);
        if (existingUsername != null)
            throw new ConflictException($"Username '{dto.Username}' is already taken.");

        var existingEmail = await _userRepo.GetByEmailAsync(dto.Email);
        if (existingEmail != null)
            throw new ConflictException($"Email '{dto.Email}' is already registered.");

        var user = new User
        {
            Username = dto.Username,
            FullName = dto.FullName,
            Email = dto.Email,
            Role = dto.Role,
            Department = dto.Department,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _hasher.HashPassword(user, dto.Password);

        await _userRepo.AddAsync(user);
        await _uow.SaveChangesAsync();

        return MapToDto(user);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userRepo.GetByUsernameAsync(dto.Username);
        if (user == null)
            throw new UnauthorizedAccessException("Invalid username or password.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid username or password.");

        var token = _tokenService.GenerateToken(user);

        return new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            Role = user.Role.ToString(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes)
        };
    }

    private static UserDto MapToDto(User user) => new()
    {
        UserId = user.UserId,
        Username = user.Username,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToString(),
        Department = user.Department,
        CreatedAt = user.CreatedAt
    };
}
