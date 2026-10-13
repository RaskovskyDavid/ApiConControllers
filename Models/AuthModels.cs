using System.ComponentModel.DataAnnotations;
namespace ApiConControllers.Models;

public record RegisterRequest(
    [Required] string FirstName,
    [Required] string LastName,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AddRoleRequest(
    [Required, EmailAddress] string Email,
    [Required] string Role);

public record SetClaimRequest(
    [Required, EmailAddress] string Email,
    [Required] string Type,
    [Required] string Value);

public record RefreshRequest([Required] string RefreshToken);

public record RevokeRequest([Required] string RefreshToken);

public record AuthResponse(
    string UserId,
    string Email,
    IEnumerable<string> Roles,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
