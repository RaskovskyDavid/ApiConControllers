using System.Security.Claims;
using System.Text;
using ApiConControllers.Auth;
using ApiConControllers.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
namespace ApiConControllers.Controllers;

// Mide lo que cuesta "poner los permisos dentro del token". Anónimo a propósito: los números deben
// poder reproducirse sin iniciar sesión. Es una herramienta de estudio: no lo expongas en producción.
[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class DiagnosticsController(TokenService tokenService) : ControllerBase
{
    // Valor por defecto de KestrelServerLimits.MaxRequestHeadersTotalSize.
    private const int KestrelHeaderLimitBytes = 32_768;

    /// <summary>
    /// Muestra cómo son realmente los claims de quien llama, incluido el emisor de cada uno.
    /// Útil cuando una comprobación de permisos deniega en silencio.
    /// </summary>
    [HttpGet("claims")]
    public IActionResult Claims()
    {
        if (User.Identity?.IsAuthenticated is not true)
        {
            return Ok(new { authenticated = false });
        }

        var identity = User.Identities.First();

        return Ok(new
        {
            authenticated = true,
            roleClaimType = identity.RoleClaimType,
            nameClaimType = identity.NameClaimType,
            claims = User.Claims.Select(c => new { c.Type, c.Value, c.Issuer })
        });
    }

    /// <summary>Mide el tamaño del token con 0, 10, 50, 200 y 500 permisos (o con el número indicado).</summary>
    [HttpGet("token-size")]
    public IActionResult TokenSize([FromQuery] int? permissions)
    {
        var counts = permissions is > 0 ? [permissions.Value] : new[] { 0, 10, 50, 200, 500 };

        return Ok(new
        {
            kestrelHeaderLimitBytes = KestrelHeaderLimitBytes,
            note = "headerBytes incluye el prefijo 'Authorization: Bearer ' y el CRLF, que es lo que "
                 + "realmente cuenta contra el límite de cabeceras del servidor.",
            results = counts.Select(Measure)
        });
    }

    private object Measure(int permissionCount)
    {
        // Los mismos claims base que produce un login real.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "admin@example.com"),
            new(JwtRegisteredClaimNames.Name, "Admin Root"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", Roles.Admin)
        };

        // Permisos realistas: "Permissions.<Módulo>.<Acción>".
        for (var i = 0; i < permissionCount; i++)
        {
            var module = $"Module{i / 4:D3}";
            var action = (i % 4) switch
            {
                0 => "View",
                1 => "Create",
                2 => "Edit",
                _ => "Delete"
            };
            claims.Add(new Claim(Permissions.ClaimType, $"{Permissions.Prefix}{module}.{action}"));
        }

        var token = tokenService.BuildToken(claims, DateTime.UtcNow.AddMinutes(60));

        var tokenBytes = Encoding.UTF8.GetByteCount(token);
        // "Authorization: Bearer " = 22 caracteres, CRLF = 2.
        var headerBytes = tokenBytes + 24;

        return new
        {
            permissions = permissionCount,
            tokenBytes,
            headerBytes,
            percentOfKestrelLimit = Math.Round(headerBytes * 100.0 / KestrelHeaderLimitBytes, 1),
            fitsInKestrelDefault = headerBytes <= KestrelHeaderLimitBytes
        };
    }
}
