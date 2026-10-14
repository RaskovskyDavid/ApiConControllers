using ApiConControllers.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ApiConControllers.Controllers;

// Endpoints de demostración de autorización basada en CLAIMS.
// Cada uno usa una política distinta de Auth/Policies.cs.
[Authorize]
[ApiController]
[Route("[controller]")]
public class ReportsController : ControllerBase
{
    /// <summary>
    /// Muestra los claims del token con el que llamas. Útil para depurar. Cualquier usuario autenticado.
    /// </summary>
    [HttpGet("me")]
    public IActionResult Me() =>
        Ok(User.Claims.Select(c => new { c.Type, c.Value }));

    /// <summary>Cualquier departamento real (engineering u operations). No sirve "none".</summary>
    [HttpGet("department")]
    [Authorize(Policy = Policies.HasDepartment)]
    public IActionResult Department() => Ok(new { report = "Reporte interno de departamentos" });

    /// <summary>Solo el departamento engineering.</summary>
    [HttpGet("engineering")]
    [Authorize(Policy = Policies.EngineeringOnly)]
    public IActionResult Engineering() => Ok(new { report = "Reporte de ingeniería" });

    /// <summary>Suscripción standard o superior.</summary>
    [HttpGet("standard")]
    [Authorize(Policy = Policies.StandardOrAbove)]
    public IActionResult Standard() => Ok(new { report = "Reporte estándar" });

    /// <summary>Solo suscripción premium.</summary>
    [HttpGet("premium")]
    [Authorize(Policy = Policies.PremiumOnly)]
    public IActionResult Premium() => Ok(new { report = "Reporte premium" });

    /// <summary>Rol Admin Y departamento engineering a la vez.</summary>
    [HttpGet("engineering-admin")]
    [Authorize(Policy = Policies.EngineeringAdmin)]
    public IActionResult EngineeringAdmin() => Ok(new { report = "Reporte de administración de ingeniería" });
}
