namespace ApiConControllers.Data;

// Mapea la sección "Seed" de appsettings.json:
// "Seed": { "RolePermissions": { "Admin": [ "Permissions.Products.View", ... ] }, "Users": [ ... ] }
public class SeedOptions
{
    public const string SectionName = "Seed";

    // Permisos que se conceden a cada ROL (como claims del rol). Clave = nombre del rol.
    public Dictionary<string, List<string>> RolePermissions { get; set; } = [];

    public List<SeedUser> Users { get; set; } = [];
}

public class SeedUser
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    // Opcionales: si vienen vacíos, el seeder no toca ese claim.
    public string Department { get; set; } = string.Empty;
    public string Subscription { get; set; } = string.Empty;
}
