using System.ComponentModel.DataAnnotations;
namespace ApiConControllers.Models;

public record Product(int Id, string Name, decimal Price);

public record SaveProductRequest([Required] string Name, decimal Price);

// Superficie de administración para gestionar lo que puede hacer un rol, sin redesplegar.
public record RolePermissionsResponse(string Role, IEnumerable<string> Permissions);

public record PermissionRequest([Required] string Permission);
