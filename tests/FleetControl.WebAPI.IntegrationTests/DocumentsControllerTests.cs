using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace FleetControl.WebAPI.IntegrationTests;

public class DocumentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DocumentsControllerTests(CustomWebApplicationFactory factory)
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

    private static MultipartFormDataContent BuildUploadForm(Guid vehicleId, string contentType = "application/pdf")
    {
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 contenido de prueba"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent
        {
            { fileContent, "file", "soat.pdf" },
            { new StringContent(vehicleId.ToString()), "vehicleId" },
            { new StringContent("Soat"), "documentType" },
            { new StringContent("2026-01-01"), "issueDate" },
            { new StringContent("2026-12-31"), "expirationDate" }
        };
        return form;
    }

    [Fact]
    public async Task Upload_SinToken_DebeRetornar401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/documents/upload", BuildUploadForm(_factory.VehicleTenantAId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.PostAsync("/api/documents/upload", BuildUploadForm(_factory.VehicleTenantAId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_ArchivoNoPdf_DebeRetornar400()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.PostAsync("/api/documents/upload", BuildUploadForm(_factory.VehicleTenantAId, "image/png"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ComoAdmin_ConArchivoValido_DebeRetornar200YCalcularHash()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.PostAsync("/api/documents/upload", BuildUploadForm(_factory.VehicleTenantAId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadedDocumentResponse>();
        body.Should().NotBeNull();
        body!.FileHashSha256.Should().NotBeNullOrWhiteSpace();
        body.VehicleId.Should().Be(_factory.VehicleTenantAId);
    }

    [Fact]
    public async Task Upload_VehiculoDeOtroTenant_DebeRetornar404()
    {
        // El admin de tenant A intenta subir un documento a un vehiculo de tenant B:
        // gracias al Global Query Filter, es indistinguible de un vehiculo inexistente.
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.PostAsync("/api/documents/upload", BuildUploadForm(_factory.VehicleTenantBId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByVehicle_ComoAdmin_DebeRetornar200()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync($"/api/documents/vehicle/{_factory.VehicleTenantAId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDownloadUrl_DocumentoInexistente_DebeRetornar404()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync($"/api/documents/{Guid.NewGuid()}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record UploadedDocumentResponse(Guid Id, Guid VehicleId, string FileHashSha256);
}
