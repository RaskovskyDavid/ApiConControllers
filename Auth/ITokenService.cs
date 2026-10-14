using System;
using System.Security.Claims;
using ApiConControllers.Entities;
namespace ApiConControllers.Auth;

public interface ITokenService
{
    // Builds a signed JWT (access token) for the user and returns it with its expiry time.
    // "claims" are the user's own claims (department, subscription...) stored in Identity.
    // "permissions" only travel INSIDE the token when PermissionSettings.Source is Token.
    (string Token, DateTime ExpiresAt) CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        IEnumerable<Claim> claims,
        IEnumerable<string> permissions);

    // Builds a random, opaque refresh token. It is NOT a JWT.
    string CreateRefreshToken();

    // SHA-256 of a refresh token. This is what gets stored in the database.
    string HashToken(string token);
}
