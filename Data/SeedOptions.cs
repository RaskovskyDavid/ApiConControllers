namespace ApiConControllers.Data;

// Mapea la sección "Seed" de appsettings.json:
// "Seed": { "Users": [ { "Email": "...", "Password": "...", "Role": "Admin", ... } ] }
public class SeedOptions
{
    public const string SectionName = "Seed";

    public List<SeedUser> Users { get; set; } = [];
}

public class SeedUser
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
