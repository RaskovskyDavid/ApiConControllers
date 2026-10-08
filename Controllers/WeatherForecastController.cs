using Microsoft.AspNetCore.Mvc;

namespace ApiConControllers.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];
    private static WeatherForecast[] ListWeatherForecast;
    private ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
        ListWeatherForecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
    /// <summary>
    /// Obtiene la lista de pronósticos del clima
    /// </summary>
    /// <returns>Retorna la lista de pronósticos del clima</returns>
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        _logger.LogInformation("Obteniendo la lista de pronósticos del clima");
        return ListWeatherForecast;
    }
    /// <summary>
    /// Obtiene un pronóstico del clima por su POSICION en la lista de pronósticos del clima
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public ActionResult<WeatherForecast> Get(int id)
    {
        var weatherForecast = ListWeatherForecast[id];
        if (weatherForecast == null)
        {
            return BadRequest();
        }
        return Ok(weatherForecast);
    }
}
