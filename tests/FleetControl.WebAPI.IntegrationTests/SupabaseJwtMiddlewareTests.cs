using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace FleetControl.WebAPI.IntegrationTests;

/// <summary>
/// Casos limite de SupabaseJwtMiddleware, ejercitados de extremo a extremo vía
/// WebApplicationFactory: firma invalida, token vencido, "sub" no-Guid,
/// usuario no registrado y usuario inactivo.
/// </summary>
public class SupabaseJwtMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SupabaseJwtMiddlewareTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task SinHeaderAuthorization_DebeRetornar401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConFirmaInvalida_DebeRetornar401()
    {
        var client = CreateClientWithToken(TestJwtGenerator.GenerateTokenWithWrongSecret(_factory.AdminTenantAId));

        var response = await client.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConTokenVencido_DebeRetornar401()
    {
        var client = CreateClientWithToken(TestJwtGenerator.GenerateExpiredToken(_factory.AdminTenantAId));

        var response = await client.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConSubNoGuid_DebeRetornar401()
    {
        var client = CreateClientWithToken(TestJwtGenerator.GenerateTokenWithInvalidSub());

        var response = await client.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConUsuarioNoRegistrado_DebeRetornar401()
    {
        var client = CreateClientWithToken(TestJwtGenerator.GenerateToken(Guid.NewGuid()));

        var response = await client.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConUsuarioInactivo_DebeRetornar401()
    {
        var client = CreateClientWithToken(TestJwtGenerator.GenerateToken(_factory.InactiveUserTenantAId));

        var response = await client.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RutaDeHealthCheck_NoRequiereToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
