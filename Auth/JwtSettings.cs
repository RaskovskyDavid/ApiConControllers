using System;

namespace ApiConControllers.Auth;
// Strongly-typed version of the "JwtSettings" section in appsettings.json.
public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    // Lifetime of the ACCESS token (the JWT). Keep it short.
    public int ExpiryMinutes { get; set; }
    // Lifetime of the REFRESH token. Long, because it is stored server-side and can be revoked.
    public int RefreshTokenDays { get; set; }
}
