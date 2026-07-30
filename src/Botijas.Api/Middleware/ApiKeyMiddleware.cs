namespace Botijas.Api.Middleware;

public class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly string? _apiKey;
    private readonly bool _isDevelopment;

    public ApiKeyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _next = next;
        _apiKey = configuration["API_KEY"];
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            if (_isDevelopment)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { error = "API key not configured" });
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            !string.Equals(providedKey, _apiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow Swagger UI and OpenAPI docs without an API key.
        return path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }
}
