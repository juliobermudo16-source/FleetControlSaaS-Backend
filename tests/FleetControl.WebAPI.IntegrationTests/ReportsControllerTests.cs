using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace FleetControl.WebAPI.IntegrationTests;

public class ReportsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        var token = TestJwtGenerator.GenerateToken(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetFleetPdf_SinToken_DebeRetornar401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/fleet/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFleetPdf_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.GetAsync("/api/reports/fleet/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFleetPdf_ComoAdmin_DebeRetornar200ConPdfValido()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync("/api/reports/fleet/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
        Encoding_HeaderStartsWithPdfMagicNumber(bytes).Should().BeTrue();
    }

    [Fact]
    public async Task GetFleetExcel_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.GetAsync("/api/reports/fleet/excel");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFleetExcel_ComoAdmin_DebeRetornar200ConArchivoNoVacio()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync("/api/reports/fleet/excel");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    private static bool Encoding_HeaderStartsWithPdfMagicNumber(byte[] bytes) =>
        bytes.Length > 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F';
}
