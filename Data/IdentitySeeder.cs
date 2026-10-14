using ApiConControllers.Auth;
using ApiConControllers.Authorization;
using ApiConControllers.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace ApiConControllers.Data;

// Deja la base lista para usar: esquema, roles, permisos de los roles, usuarios y claims iniciales.
// Es IDEMPOTENTE y CONVERGENTE: describe el estado deseado y lo reconcilia en cada arranque.
// Crea lo que falta y corrige lo que difiere, sin duplicar.
// OJO: los permisos solo se AÑADEN. Si revocas uno en caliente con la API y reinicias, el seed lo
// vuelve a conceder mientras siga en appsettings.json.
public class IdentitySeeder(
    AppDbContext db,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    PermissionStore permissionStore,
    IOptions<SeedOptions> options,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync()
    {
        await db.Database.EnsureCreatedAsync();
        await SeedRolesAsync();
        await SeedRolePermissionsAsync();
        await SeedUsersAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            LogResult(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
        }
    }

    // Los permisos se conceden a los ROLES, como claims del propio rol (AspNetRoleClaims).
    private async Task SeedRolePermissionsAsync()
    {
        foreach (var (role, permissions) in options.Value.RolePermissions)
        {
            if (!Roles.All.Contains(role))
            {
                logger.LogWarning("Seed permissions for role '{Role}' skipped: unknown role.", role);
                continue;
            }

            // Cada entrada puede ser un permiso o un comodín de módulo ("Permissions.Orders.*").
            foreach (var permission in permissions.SelectMany(Permissions.Expand))
            {
                if (!Permissions.All.Contains(permission))
                {
                    logger.LogWarning("Seed permission '{Permission}' for role '{Role}' skipped: unknown permission.", permission, role);
                    continue;
                }

                // GrantAsync es idempotente y expulsa la caché del store.
                if (!await permissionStore.GrantAsync(role, permission))
                {
                    logger.LogError("Seed failed to grant '{Permission}' to role '{Role}': role not found.", permission, role);
                }
            }

            foreach (var wildcard in permissions.Where(p => p.EndsWith(".*", StringComparison.Ordinal) && !Permissions.Expand(p).Any()))
            {
                logger.LogWarning("Seed wildcard '{Wildcard}' for role '{Role}' skipped: it matches no permission.", wildcard, role);
            }
        }
    }

    private async Task SeedUsersAsync()
    {
        foreach (var seed in options.Value.Users)
        {
            if (FindProblem(seed) is { } problem)
            {
                logger.LogWarning("Seed user '{Email}' skipped: {Problem}.", seed.Email, problem);
                continue;
            }

            var user = await userManager.FindByEmailAsync(seed.Email) ?? await CreateUserAsync(seed);
            if (user is null)
            {
                continue;
            }

            await EnsureRoleAsync(user, seed.Role);
            await EnsureClaimAsync(user, AppClaimTypes.Department, seed.Department);
            await EnsureClaimAsync(user, AppClaimTypes.Subscription, seed.Subscription);
        }
    }

    // Un valor desconocido (rol, departamento, suscripción) hace que se omita el usuario con una
    // advertencia, en lugar de sembrar datos que ninguna política reconoce.
    private static string? FindProblem(SeedUser seed)
    {
        if (!Roles.All.Contains(seed.Role))
        {
            return $"unknown role '{seed.Role}'";
        }

        if (!IsValidOptionalClaim(AppClaimTypes.Department, seed.Department))
        {
            return $"unknown department '{seed.Department}'";
        }

        if (!IsValidOptionalClaim(AppClaimTypes.Subscription, seed.Subscription))
        {
            return $"unknown subscription '{seed.Subscription}'";
        }

        return null;
    }

    private static bool IsValidOptionalClaim(string type, string value) =>
        value.Length == 0 || AppClaimTypes.IsValid(type, value);

    private async Task<ApplicationUser?> CreateUserAsync(SeedUser seed)
    {
        var user = new ApplicationUser
        {
            UserName = seed.Email,
            Email = seed.Email,
            FirstName = seed.FirstName,
            LastName = seed.LastName
        };

        var created = await userManager.CreateAsync(user, seed.Password);
        return LogResult(created, $"create seed user '{seed.Email}'") ? user : null;
    }

    private async Task EnsureRoleAsync(ApplicationUser user, string role)
    {
        if (!await userManager.IsInRoleAsync(user, role))
        {
            LogResult(await userManager.AddToRoleAsync(user, role), $"add '{user.Email}' to role '{role}'");
        }
    }

    private async Task EnsureClaimAsync(ApplicationUser user, string type, string value)
    {
        if (value.Length > 0)
        {
            LogResult(await userManager.SetClaimAsync(user, type, value), $"set claim '{type}' for '{user.Email}'");
        }
    }

    // Antes los fallos del seed eran silenciosos (p. ej. una contraseña que incumple la política).
    private bool LogResult(IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return true;
        }

        logger.LogError("Seed failed to {Action}: {Errors}", action,
            string.Join("; ", result.Errors.Select(e => e.Description)));
        return false;
    }
}

public static class IdentitySeederExtensions
{
    public static IServiceCollection AddIdentitySeeder(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<IdentitySeeder>();
        return services;
    }

    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        // El seeder es "scoped" (usa DbContext): necesita su propio scope fuera de una petición HTTP.
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
    }
}
