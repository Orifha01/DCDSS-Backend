using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DCDSS.Configuration;
using DCDSS.Entities;
using DCDSS.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DCDSS.Services;

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> opts)
    {
        _settings = opts.Value;
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim("sub", user.UserId.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim("name", user.Username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
