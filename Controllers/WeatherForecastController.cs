using ApiConControllers.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ApiConControllers.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    // Se inicializa UNA vez. Antes se recreaba en el constructor, que corre en cada petición,
    // así que Delete nunca tenía efecto visible.
    private static WeatherForecast[] ListWeatherForecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
    {
        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        TemperatureC = Random.Shared.Next(-20, 55),
        Summary = Summaries[Random.Shared.Next(Summaries.Length)]
    })
    .ToArray();

    /// <summary>
    /// Obtiene la lista de pronósticos del clima. Manager o Admin.
    /// </summary>
    /// <returns>Retorna la lista de pronósticos del clima</returns>
    [HttpGet(Name = "GetWeatherForecast")]
    [Authorize(Policy = Policies.ManagerOrAbove)]
    public IEnumerable<WeatherForecast> Get([FromServices] ILogger<WeatherForecastController> _logger)
    {
        _logger.LogInformation("Obteniendo la lista de pronósticos del clima");
        return ListWeatherForecast;
    }

    /// <summary>
    /// Obtiene un pronóstico del clima por su POSICION en la lista. Cualquier rol (User, Manager o Admin).
    /// </summary>
    /// <param name="id">Posición en la lista, empezando en 0</param>
    [HttpGet("{id}")]
    [Authorize(Policy = Policies.UserOrAbove)]
    public ActionResult<WeatherForecast> Get(int id)
    {
        if (id < 0 || id >= ListWeatherForecast.Length)
        {
            return NotFound();
        }
        return Ok(ListWeatherForecast[id]);
    }

    /// <summary>
    /// Elimina un pronóstico del clima por su POSICION en la lista. Manager o Admin.
    /// </summary>
    /// <param name="id">Posición en la lista, empezando en 0</param>
    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.ManagerOrAbove)]
    public IActionResult Delete(int id)
    {
        if (id < 0 || id >= ListWeatherForecast.Length)
        {
            return NotFound();
        }

        // Borra por posición porque WeatherForecast no tiene propiedad Id.
        ListWeatherForecast = ListWeatherForecast
            .Where((_, index) => index != id)
            .ToArray();

        return NoContent();
    }
}
