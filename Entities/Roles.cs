namespace ApiConControllers.Entities;

// Nombres de rol en un solo lugar. Cualquier rol nuevo se añade aquí y en "All":
// el seeder lo crea solo, sin tocar más código.
public static class Roles
{
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static readonly string[] All = [User, Manager, Admin];
}
