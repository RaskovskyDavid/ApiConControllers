namespace ApiConControllers.Entities;

// Nombres de los claims de la app, en un solo lugar para no cometer typos.
// Los tipos Y los valores de un claim distinguen mayúsculas: "Department" y "department" son claims distintos.
// Los permisos NO son claims: se calculan en cada petición desde las membresías (ver DepartmentMembership).
public static class AppClaimTypes
{
    public const string Department = "department";
    public const string Subscription = "subscription";

    // Valores permitidos por cada claim. Sirve para validar el seed y el endpoint set-claim.
    private static readonly Dictionary<string, string[]> AllowedValues = new()
    {
        [Department] = Departments.All,
        [Subscription] = Subscriptions.All
    };

    public static bool IsKnownType(string type) => AllowedValues.ContainsKey(type);

    public static bool IsValid(string type, string value) =>
        AllowedValues.TryGetValue(type, out var values) && values.Contains(value);
}
