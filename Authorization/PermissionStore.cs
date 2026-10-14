using System.Collections.Concurrent;
using System.Security.Claims;
using ApiConControllers.Entities;
using Microsoft.AspNetCore.Identity;

namespace ApiConControllers.Authorization;

// La fuente de verdad de "qué permisos tiene este rol".
//
// Los permisos viven como claims del ROL (AspNetRoleClaims), no del usuario. Eso es lo que los hace
// administrables en tiempo de ejecución: se concede un permiso a Manager una vez y todos los managers
// lo tienen en su siguiente petición.
//
// Las lecturas pasan por una caché porque el handler consulta esto en cada petición autorizada.
// Conceder y revocar escriben y expulsan la entrada de la caché.
public class PermissionStore(IServiceScopeFactory scopeFactory)
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Resuelve la unión de permisos de todos los roles de quien llama.
    public async Task<HashSet<string>> GetForRolesAsync(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            permissions.UnionWith(await GetForRoleAsync(role));
        }

        return permissions;
    }

    public async Task<HashSet<string>> GetForRoleAsync(string role)
    {
        if (_cache.TryGetValue(role, out var cached))
        {
            return cached;
        }

        var permissions = await LoadFromIdentityAsync(role);

        // En una app real aquí va HybridCache con un TTL corto, para que la consulta sobreviva a un
        // reinicio y se comparta entre instancias.
        _cache[role] = permissions;
        return permissions;
    }

    public async Task<bool> GrantAsync(string role, string permission)
    {
        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var identityRole = await roleManager.FindByNameAsync(role);
        if (identityRole is null)
        {
            return false;
        }

        var existing = await roleManager.GetClaimsAsync(identityRole);
        if (existing.Any(c => c.Type == Permissions.ClaimType && c.Value == permission))
        {
            return true;
        }

        await roleManager.AddClaimAsync(identityRole, new Claim(Permissions.ClaimType, permission));

        // Expulsa la caché para que la siguiente petición lea la concesión nueva.
        _cache.TryRemove(role, out _);
        return true;
    }

    public async Task<bool> RevokeAsync(string role, string permission)
    {
        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var identityRole = await roleManager.FindByNameAsync(role);
        if (identityRole is null)
        {
            return false;
        }

        var claim = (await roleManager.GetClaimsAsync(identityRole))
            .FirstOrDefault(c => c.Type == Permissions.ClaimType && c.Value == permission);

        if (claim is not null)
        {
            await roleManager.RemoveClaimAsync(identityRole, claim);
        }

        _cache.TryRemove(role, out _);
        return true;
    }

    private async Task<HashSet<string>> LoadFromIdentityAsync(string role)
    {
        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var identityRole = await roleManager.FindByNameAsync(role);
        if (identityRole is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var claims = await roleManager.GetClaimsAsync(identityRole);

        return claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
