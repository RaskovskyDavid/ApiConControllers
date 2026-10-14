using System.Security.Claims;
using ApiConControllers.Auth;
using ApiConControllers.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ApiConControllers.Authorization;

// Decide si quien llama tiene el permiso que pidió el endpoint.
public class PermissionAuthorizationHandler(
    PermissionStore store,
    IOptions<PermissionSettings> settings)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Sin identidad no hay permisos. Dejar el requisito sin cumplir produce un 401 y no un 403,
        // que es lo que debe ver un llamador anónimo.
        if (context.User.Identity?.IsAuthenticated is not true)
        {
            return;
        }

        var granted = settings.Value.Source == PermissionSource.Token
            ? ReadFromToken(context.User)
            : await store.GetForRolesAsync(ReadRoles(context.User));

        if (granted.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        // Sin context.Fail(): otro handler del mismo requisito aún puede cumplirlo. Fail() los vetaría a todos.
    }

    private static HashSet<string> ReadFromToken(ClaimsPrincipal user) =>
        user.FindAll(Permissions.ClaimType)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Los roles se leen con el RoleClaimType configurado en la identidad. Fijar ClaimTypes.Role a mano
    // se rompe cuando MapInboundClaims es false y el token trae el claim corto "role".
    private static HashSet<string> ReadRoles(ClaimsPrincipal user) =>
        user.Identities
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
