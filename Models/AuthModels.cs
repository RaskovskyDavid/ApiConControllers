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

// Permissions: lo que el usuario puede hacer HOY según sus roles. Viaja dentro del token solo si
// Permissions:Source es Token; en Lookup se muestra aquí pero se consulta en cada petición.
public record AuthResponse(
    string UserId,
    string Email,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
