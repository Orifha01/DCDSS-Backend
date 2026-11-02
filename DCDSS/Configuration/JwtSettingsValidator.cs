using Microsoft.Extensions.Options;

namespace DCDSS.Configuration;

public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey)
            || options.SigningKey == "REPLACE_ME"
            || System.Text.Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            return ValidateOptionsResult.Fail(
                "JwtSettings:SigningKey must be at least 32 UTF-8 bytes and must not be the placeholder 'REPLACE_ME'.");
        }

        return ValidateOptionsResult.Success;
    }
}
