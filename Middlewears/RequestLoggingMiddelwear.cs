public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log the incoming request
        _logger.LogInformation("Incoming request: {method} {url}", context.Request.Method, context.Request.Path);
        Console.WriteLine($"Incoming request: {context.Request.Method} {context.Request.Path}");
        // Call the next middleware in the pipeline
        await _next(context);

        // Log the outgoing response
        _logger.LogInformation("Outgoing response: {statusCode}", context.Response.StatusCode);
        Console.WriteLine($"Outgoing response: {context.Response.StatusCode}");
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}