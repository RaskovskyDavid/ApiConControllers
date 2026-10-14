using Microsoft.AspNetCore.Authorization;

namespace ApiConControllers.Authorization;

// Un solo tipo de requisito cubre todos los permisos del sistema. El permiso exigido viaja como
// dato en la instancia, no como una clase nueva por regla.
public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
