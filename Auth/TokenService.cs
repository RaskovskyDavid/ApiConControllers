using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiConControllers.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
namespace ApiConControllers.Auth;

public class TokenService(
    IOptions<JwtSettings> jwtSettings,
    IOptions<PermissionSettings> permissionSettings) : ITokenService
{
    private readonly JwtSettings _settings = jwtSettings.Value;
    private readonly PermissionSettings _permissions = permissionSettings.Value;

    public (string Token, DateTime ExpiresAt) CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        IEnumerable<Claim> userClaims,
        IEnumerable<string> permissions)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        // Standard identity claims - who the user is.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Roles are claims too - one "role" claim per role. Always cheap: a user has a handful of roles.
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        // The user's own claims (department, subscription...) travel inside the token too.
        claims.AddRange(userClaims);

        // Only stamp permissions into the token when configured to. This is the strategy that makes
        // tokens grow - measure it at /diagnostics/token-size.
        if (_permissions.Source == PermissionSource.Token)
        {
            claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));
        }

        return (BuildToken(claims, expiresAt), expiresAt);
    }

    // Shared by CreateAccessToken and the diagnostics probe so the measured size comes from the
    // exact same signing path a real login uses.
    internal string BuildToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        // The signing key proves the token came from us and was not tampered with.
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = credentials
        };

        // JsonWebTokenHandler is the modern, faster handler that replaces JwtSecurityTokenHandler.
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public string CreateRefreshToken()
    {
        // A refresh token is just a large cryptographically-random value.
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashToken(string token)
    {
        // The refresh token is already high-entropy random data, so a plain SHA-256 is enough
        // (no salt or slow hash needed, unlike passwords).
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
