using ApiConControllers.Authorization;
using ApiConControllers.Entities;
using ApiConControllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ApiConControllers.Controllers;

// Esta es la recompensa de todo el patrón: cambiar lo que puede hacer un rol es una llamada a la API,
// no un redespliegue. La autorización por roles no puede hacerlo porque los nombres de rol están
// compilados en los endpoints.
[ApiController]
[Route("admin/roles")]
[Authorize(Policy = Permissions.Products.Edit)]
public class RolePermissionsController(PermissionStore store) : ControllerBase
{
    /// <summary>Permisos que tiene hoy un rol.</summary>
    [HttpGet("{role}/permissions")]
    public async Task<ActionResult<RolePermissionsResponse>> Get(string role)
    {
        var permissions = await store.GetForRoleAsync(role);
        return Ok(new RolePermissionsResponse(role, permissions.Order()));
    }

    /// <summary>Concede un permiso a un rol. Vale para todos sus usuarios.</summary>
    [HttpPost("{role}/permissions")]
    public async Task<IActionResult> Grant(string role, PermissionRequest request)
    {
        if (!Permissions.All.Contains(request.Permission))
        {
            return BadRequest($"Unknown permission '{request.Permission}'.");
        }

        return await store.GrantAsync(role, request.Permission)
            ? Ok($"Granted '{request.Permission}' to '{role}'.")
            : NotFound($"Role '{role}' not found.");
    }

    /// <summary>Revoca un permiso de un rol.</summary>
    [HttpDelete("{role}/permissions")]
    public async Task<IActionResult> Revoke(string role, [FromBody] PermissionRequest request) =>
        await store.RevokeAsync(role, request.Permission)
            ? Ok($"Revoked '{request.Permission}' from '{role}'.")
            : NotFound($"Role '{role}' not found.");
}
