using System;
using System.Net.Http.Headers;
using System.Text;
namespace ApiConControllers.Middlewears;

public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    //usuario y contraseña hardodeados para prueba
    private const string DemoUsername = "admin";
    private const string DemoPassword = "admin";
    public BasicAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            if (context.Request.Path.Value.Contains("swagger")
                   || context.Request.Path.Value.Contains("scalar")
                   || context.Request.Path.Value.Contains("openapi"))
            {
                await _next(context);
                return;
            }
            else if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("Authorization header missing");
                return;
            }

            var authHeader = AuthenticationHeaderValue.Parse(context.Request.Headers["Authorization"]);
            // Decodificar credenciales base64
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

            // var credentials = authHeader.Split(' ')[1]; // Assuming "Basic <credentials>"
            // var decodedCredentials = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(credentials));
            // var parts = decodedCredentials.Split(':');
            // var username = parts[0];
            // var password = parts[1];
            var username = credentials[0];
            var password = credentials[1];
            // Validar usuario/contraseña
            if (username == DemoUsername && password == DemoPassword)
            {
                // Usuario autenticado → continuar pipeline
                await _next(context);
                return;
            }

            if (username != DemoUsername || password != DemoPassword)
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("Invalid username or password");
                return;
            }

            // Si llega aquí → no autorizado
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Missing or invalid credentials.");

        }

        catch (Exception ex)
        {
            context.Response.StatusCode = 500; // Internal Server Error
            await context.Response.WriteAsync($"An error occurred: {ex.Message}");
        }
    }


}

public static class BasicAuthExtensions
{
    public static IApplicationBuilder UseBasicAuth(this IApplicationBuilder app)
    {
        return app.UseMiddleware<BasicAuthMiddleware>();
    }
}