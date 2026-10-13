namespace ApiConControllers.Entities;

// Valores posibles del claim "department".
public static class Departments
{
    public const string Engineering = "engineering";
    public const string Operations = "operations";
    public const string None = "none";   // OJO: es un valor real, el claim existe. Ver políticas.

    public static readonly string[] All = [Engineering, Operations, None];
}

// Valores posibles del claim "subscription", de menor a mayor nivel.
public static class Subscriptions
{
    public const string Free = "free";
    public const string Standard = "standard";
    public const string Premium = "premium";

    // El orden de este arreglo define la jerarquía: free < standard < premium.
    public static readonly string[] All = [Free, Standard, Premium];

    // Posición en la jerarquía; -1 si el valor no existe o es nulo.
    public static int Rank(string? subscription) =>
        subscription is null ? -1 : Array.IndexOf(All, subscription);
}
