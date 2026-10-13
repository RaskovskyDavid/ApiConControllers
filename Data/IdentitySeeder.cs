using ApiConControllers.Auth;
using ApiConControllers.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace ApiConControllers.Data;

// Deja la base lista para usar: esquema, roles, usuarios y claims iniciales.
// Es IDEMPOTENTE y CONVERGENTE: describe el estado deseado y lo reconcilia en cada arranque.
// Crea lo que falta y corrige lo que difiere, sin duplicar. Así también funciona con una base
// que ya tenía los usuarios de un arranque anterior (sin claims).
public class IdentitySeeder(
    AppDbContext db,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<SeedOptions> options,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync()
    {
        await db.Database.EnsureCreatedAsync();
        await SeedRolesAsync();
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

    private async Task SeedUsersAsync()
    {
        foreach (var seed in options.Value.Users)
        {
            if (!IsValid(seed))
            {
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

    // Un valor desconocido (rol, departamento, suscripción) se omite con una advertencia
    // en lugar de sembrar datos que ninguna política reconoce.
    private bool IsValid(SeedUser seed)
    {
        var problem = !Roles.All.Contains(seed.Role) ? $"unknown role '{seed.Role}'"
            : !IsValidOptionalClaim(AppClaimTypes.Department, seed.Department) ? $"unknown department '{seed.Department}'"
            : !IsValidOptionalClaim(AppClaimTypes.Subscription, seed.Subscription) ? $"unknown subscription '{seed.Subscription}'"
            : null;

        if (problem is not null)
        {
            logger.LogWarning("Seed user '{Email}' skipped: {Problem}.", seed.Email, problem);
        }

        return problem is null;
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
