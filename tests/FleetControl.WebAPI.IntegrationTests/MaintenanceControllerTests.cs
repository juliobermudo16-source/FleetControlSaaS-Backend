using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace FleetControl.WebAPI.IntegrationTests;

public class MaintenanceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MaintenanceControllerTests(CustomWebApplicationFactory factory)
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
    public async Task Register_SinToken_DebeRetornar401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/maintenance", new
        {
            vehicleId = _factory.VehicleTenantAId,
            maintenanceTypeId = _factory.MaintenanceTypeId,
            mileageAtService = 12000,
            serviceDate = "2026-01-01",
            cost = 150,
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.PostAsJsonAsync("/api/maintenance", new
        {
            vehicleId = _factory.VehicleTenantAId,
            maintenanceTypeId = _factory.MaintenanceTypeId,
            mileageAtService = 12000,
            serviceDate = "2026-01-01",
            cost = 150,
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_ComoAdmin_VehiculoInexistente_DebeRetornar404()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.PostAsJsonAsync("/api/maintenance", new
        {
            vehicleId = Guid.NewGuid(),
            maintenanceTypeId = _factory.MaintenanceTypeId,
            mileageAtService = 12000,
            serviceDate = "2026-01-01",
            cost = 150,
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_ComoAdmin_ConDatosValidos_DebeRetornar200()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.PostAsJsonAsync("/api/maintenance", new
        {
            vehicleId = _factory.VehicleTenantAId,
            maintenanceTypeId = _factory.MaintenanceTypeId,
            mileageAtService = 12000,
            serviceDate = "2026-01-01",
            cost = 150,
            notes = "cambio de rutina"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MaintenanceLogResponse>();
        body!.MaintenanceTypeName.Should().Be("Cambio de aceite");
    }

    [Fact]
    public async Task GetStatus_VehiculoInexistente_DebeRetornar404()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync($"/api/maintenance/vehicle/{Guid.NewGuid()}/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStatus_ComoAdmin_DebeRetornar200ConElTipoDeMantenimientoGlobal()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync($"/api/maintenance/vehicle/{_factory.VehicleTenantAId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<MaintenanceStatusResponse>>();
        body.Should().ContainSingle(m => m.MaintenanceTypeId == _factory.MaintenanceTypeId);
    }

    [Fact]
    public async Task GetStatus_ComoConductorDeOtroTenant_DebeRetornar404()
    {
        // El vehiculo de tenant B es indistinguible de uno inexistente para un
        // usuario de tenant A, gracias al Global Query Filter (404, no 403).
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.GetAsync($"/api/maintenance/vehicle/{_factory.VehicleTenantBId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record MaintenanceLogResponse(Guid Id, Guid VehicleId, string MaintenanceTypeName, int MileageAtService, decimal Cost);
    private record MaintenanceStatusResponse(Guid VehicleId, Guid MaintenanceTypeId, string MaintenanceTypeName, double WearPercentage);
}
