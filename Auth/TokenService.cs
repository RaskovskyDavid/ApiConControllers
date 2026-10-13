using ApiConControllers.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
namespace ApiConControllers.Auth;

public class TokenService(IOptions<JwtSettings> jwtSettings) : ITokenService

{
    private readonly JwtSettings _settings = jwtSettings.Value;
    public (string Token, DateTime ExpiresAt) CreateAccessToken(
        ApplicationUser user, IEnumerable<string> roles, IEnumerable<Claim> userClaims)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        // Claims are the pieces of information we store inside the token.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // One "role" claim per role the user has.
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        // The user's own claims (department, subscription...) travel inside the token too.
        claims.AddRange(userClaims);

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
        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return (token, expiresAt);
    }
    public string CreateRefreshToken()
    {
        // A refresh token is just a large cryptographically-random value.
        // RandomNumberGenerator replaces the obsolete RNGCryptoServiceProvider.
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
