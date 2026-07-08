using System.Text.Json;
using FleetControl.Application.Exceptions;
using FleetControl.WebAPI.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetControl.WebAPI.IntegrationTests;

/// <summary>
/// Pruebas unitarias (sin WebApplicationFactory) de ExceptionHandlingMiddleware:
/// verifica que cada tipo de excepcion de Application se traduzca al codigo
/// HTTP correcto, invocando el middleware directamente sobre un HttpContext falso.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, string Body)> InvokeAsync(Exception exceptionToThrow)
    {
        var middleware = new ExceptionHandlingMiddleware(_ => throw exceptionToThrow, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task InvokeAsync_DebeMapear_NotFoundException_A404()
    {
        var (statusCode, body) = await InvokeAsync(new NotFoundException("Vehicle", Guid.NewGuid()));

        statusCode.Should().Be(StatusCodes.Status404NotFound);
        body.Should().Contain("error");
    }

    [Fact]
    public async Task InvokeAsync_DebeMapear_ForbiddenAccessException_A403()
    {
        var (statusCode, _) = await InvokeAsync(new ForbiddenAccessException("no autorizado"));

        statusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_DebeMapear_InvalidOperationException_A400()
    {
        var (statusCode, body) = await InvokeAsync(new InvalidOperationException("kilometraje invalido"));

        statusCode.Should().Be(StatusCodes.Status400BadRequest);
        body.Should().Contain("kilometraje invalido");
    }

    [Fact]
    public async Task InvokeAsync_DebeMapear_ArgumentOutOfRangeException_A400()
    {
        var (statusCode, _) = await InvokeAsync(new ArgumentOutOfRangeException("intervalKm"));

        statusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_DebeMapear_ExcepcionNoControlada_A500_SinFiltrarElMensajeOriginal()
    {
        var (statusCode, body) = await InvokeAsync(new InvalidCastException("detalle interno sensible"));

        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
        body.Should().NotContain("detalle interno sensible"); // no debe filtrar detalles internos
        body.Should().Contain("error interno");
    }

    [Fact]
    public async Task InvokeAsync_NoDebeInterceptarNada_CuandoNoHayExcepcion()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
