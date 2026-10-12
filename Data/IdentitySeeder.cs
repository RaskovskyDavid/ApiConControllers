using ApiConControllers.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace ApiConControllers.Data;

// Deja la base lista para usar: esquema, roles y usuarios iniciales.
// Es idempotente: se puede ejecutar en cada arranque sin duplicar nada.
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

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            LogResult(result, $"create role '{role}'");
        }
    }

    private async Task SeedUsersAsync()
    {
        foreach (var seed in options.Value.Users)
        {
            if (!Roles.All.Contains(seed.Role))
            {
                logger.LogWarning("Seed user '{Email}' skipped: unknown role '{Role}'.", seed.Email, seed.Role);
                continue;
            }

            if (await userManager.FindByEmailAsync(seed.Email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = seed.Email,
                Email = seed.Email,
                FirstName = seed.FirstName,
                LastName = seed.LastName
            };

            var created = await userManager.CreateAsync(user, seed.Password);
            if (!LogResult(created, $"create seed user '{seed.Email}'"))
            {
                continue;
            }

            LogResult(await userManager.AddToRoleAsync(user, seed.Role), $"add '{seed.Email}' to role '{seed.Role}'");
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
