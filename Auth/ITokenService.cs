using System;
using ApiConControllers.Entities;
namespace ApiConControllers.Auth;

public interface ITokenService
{
    // Builds a signed JWT (access token) for the user and returns it with its expiry time.
    (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    // Builds a random, opaque refresh token. It is NOT a JWT.
    string CreateRefreshToken();

    // SHA-256 of a refresh token. This is what gets stored in the database.
    string HashToken(string token);
}
