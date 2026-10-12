using ApiConControllers.Entities;
namespace ApiConControllers.Auth;

// Políticas de autorización con nombre. Los controllers usan [Authorize(Policy = Policies.X)]
// y no repiten listas de roles; si la jerarquía cambia, se cambia aquí y en ningún otro sitio.
public static class Policies
{
    public const string UserOrAbove = nameof(UserOrAbove);       // User, Manager o Admin
    public const string ManagerOrAbove = nameof(ManagerOrAbove); // Manager o Admin
    public const string AdminOnly = nameof(AdminOnly);           // solo Admin

    public static IServiceCollection AddRolePolicies(this IServiceCollection services)
    {
        // RequireRole(a, b) significa "a O b".
        services.AddAuthorizationBuilder()
            .AddPolicy(UserOrAbove, policy => policy.RequireRole(Roles.User, Roles.Manager, Roles.Admin))
            .AddPolicy(ManagerOrAbove, policy => policy.RequireRole(Roles.Manager, Roles.Admin))
            .AddPolicy(AdminOnly, policy => policy.RequireRole(Roles.Admin));

        return services;
    }
}
