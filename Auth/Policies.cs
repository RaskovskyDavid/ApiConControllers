using ApiConControllers.Entities;
using Microsoft.AspNetCore.Authorization;
namespace ApiConControllers.Auth;

// Políticas de autorización con nombre. Los controllers usan [Authorize(Policy = Policies.X)]
// y no repiten listas de roles ni de claims; si una regla cambia, se cambia aquí y en ningún otro sitio.
public static class Policies
{
    // Basadas en ROLES
    public const string UserOrAbove = nameof(UserOrAbove);       // User, Manager o Admin
    public const string ManagerOrAbove = nameof(ManagerOrAbove); // Manager o Admin
    public const string AdminOnly = nameof(AdminOnly);           // solo Admin

    // Basadas en CLAIMS
    public const string EngineeringOnly = nameof(EngineeringOnly);     // department = engineering
    public const string HasDepartment = nameof(HasDepartment);         // cualquier departamento real (no "none")
    public const string StandardOrAbove = nameof(StandardOrAbove);     // subscription >= standard
    public const string PremiumOnly = nameof(PremiumOnly);             // subscription >= premium

    // ROL + CLAIM a la vez
    public const string EngineeringAdmin = nameof(EngineeringAdmin);   // Admin Y department = engineering

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // RequireRole(a, b) significa "a O b".
            .AddPolicy(UserOrAbove, p => p.RequireRole(Roles.User, Roles.Manager, Roles.Admin))
            .AddPolicy(ManagerOrAbove, p => p.RequireRole(Roles.Manager, Roles.Admin))
            .AddPolicy(AdminOnly, p => p.RequireRole(Roles.Admin))

            // RequireClaim(tipo, v1, v2) significa "el claim existe Y su valor es v1 O v2".
            .AddPolicy(EngineeringOnly, p => p.RequireClaim(AppClaimTypes.Department, Departments.Engineering))
            // Sin RequireClaim(tipo) a secas: "none" también es un claim y pasaría.
            .AddPolicy(HasDepartment, p => p.RequireClaim(AppClaimTypes.Department, Departments.Engineering, Departments.Operations))

            // Requisito propio: compara niveles de la jerarquía de suscripción.
            .AddPolicy(StandardOrAbove, p => p.AddRequirements(new SubscriptionRequirement(Subscriptions.Standard)))
            .AddPolicy(PremiumOnly, p => p.AddRequirements(new SubscriptionRequirement(Subscriptions.Premium)))

            // Todos los requisitos de UNA política deben cumplirse (Y).
            .AddPolicy(EngineeringAdmin, p => p
                .RequireRole(Roles.Admin)
                .RequireClaim(AppClaimTypes.Department, Departments.Engineering));

        // Un handler por requisito propio. Singleton porque no guarda estado.
        services.AddSingleton<IAuthorizationHandler, SubscriptionHandler>();

        return services;
    }
}
