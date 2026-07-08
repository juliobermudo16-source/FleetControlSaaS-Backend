using FleetControl.Application.Exceptions;
using System.Text.Json;

namespace FleetControl.WebAPI.Middleware;

/// <summary>Traduce excepciones de Application a codigos HTTP consistentes.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, message) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                ForbiddenAccessException => (StatusCodes.Status403Forbidden, ex.Message),
                InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
                ArgumentOutOfRangeException => (StatusCodes.Status400BadRequest, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "Ocurrio un error interno en el servidor.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Error no controlado");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        }
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseFleetControlExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
