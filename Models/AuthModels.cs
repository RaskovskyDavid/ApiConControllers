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

public record AuthResponse(string Token, DateTime ExpiresAt);
