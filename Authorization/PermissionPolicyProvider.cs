using ApiConControllers.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ApiConControllers.Authorization;

// Sin esto, cada permiso necesitaría su propio AddPolicy al arrancar. Con esto, CUALQUIER nombre de
// política que empiece por "Permissions." recibe una política construida a pedido: añadir un
// permiso es un cambio de datos, no de código.
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    // ASP.NET Core solo usa UN IAuthorizationPolicyProvider. Lo que este no reconoce hay que
    // entregárselo al proveedor por defecto, o dejan de resolverse las políticas con nombre
    // registradas en otras partes de la app (UserOrAbove, AdminOnly...).
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Permissions.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                // No es por los códigos de estado: 401 o 403 lo decide si la autenticación tuvo éxito.
                // Está para que un llamador anónimo no pueda satisfacer la política si un handler
                // futuro de este requisito deja de inspeccionar al usuario.
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackProvider.GetDefaultPolicyAsync();

    // Debe devolver la política FALLBACK. Devolver GetDefaultPolicyAsync() aquí (un copy-paste muy común)
    // aplica en silencio un requisito de usuario autenticado a todo endpoint que nunca lo pidió.
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackProvider.GetFallbackPolicyAsync();
}
