using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace FleetControl.WebAPI.IntegrationTests;

/// <summary>
/// Pruebas de integracion end-to-end (via WebApplicationFactory) que verifican
/// la SEGURIDAD real de los endpoints: aislamiento entre tenants y
/// restricciones por rol. Se usan JWTs de prueba firmados con el mismo
/// secreto que el middleware personalizado espera.
/// </summary>
public class VehiclesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VehiclesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        // Ensure database seeded after factory host is built
        _factory.EnsureSeeded();
        var token = TestJwtGenerator.GenerateToken(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetAll_SinToken_DebeRetornar401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_ComoAdminTenantA_NoDebeVerVehiculosDeTenantB()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync("/api/vehicles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();

        vehicles.Should().NotBeNull();
        vehicles!.Should().ContainSingle(v => v.LicensePlate == "AAA-111");
        vehicles.Should().NotContain(v => v.LicensePlate == "BBB-222"); // vehiculo del otro tenant
    }

    [Fact]
    public async Task GetById_ComoAdminTenantA_AccediendoVehiculoDeTenantB_DebeRetornar404()
    {
        // Gracias al Global Query Filter por TenantId, el vehiculo de otro
        // tenant es indistinguible de uno inexistente: 404, no 403 (no se
        // filtra informacion sobre la existencia de datos ajenos).
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync($"/api/vehicles/{_factory.VehicleTenantBId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "ZZZ-999",
            brand = "Hyundai",
            model = "Tucson",
            manufactureYear = 2023,
            color = "Negro",
            currentMileage = 0,
            assignedDriverId = (Guid?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DashboardSummary_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.GetAsync("/api/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DashboardSummary_ComoAdmin_DebeRetornar200()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync("/api/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_ComoAdminTenantA_VehiculoPropio_DebeRetornar200()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync($"/api/vehicles/{_factory.VehicleTenantAId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicle = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        vehicle!.LicensePlate.Should().Be("AAA-111");
    }

    [Fact]
    public async Task GetById_Inexistente_DebeRetornar404()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.GetAsync($"/api/vehicles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.PutAsJsonAsync($"/api/vehicles/{_factory.VehicleTenantAId}", new
        {
            brand = "Toyota",
            model = "Hilux",
            color = "Negro",
            currentMileage = 15000,
            assignedDriverId = (Guid?)null,
            status = 0 // VehicleStatus.Active (los enums se serializan como int por defecto)
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ComoAdmin_DebeRetornar200ConCambiosAplicados()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.PutAsJsonAsync($"/api/vehicles/{_factory.VehicleTenantAId}", new
        {
            brand = "Toyota",
            model = "Hilux Sport",
            color = "Negro",
            currentMileage = 15000,
            assignedDriverId = _factory.DriverTenantAId,
            status = 0 // VehicleStatus.Active (los enums se serializan como int por defecto)
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicle = await response.Content.ReadFromJsonAsync<VehicleUpdateResponse>();
        vehicle!.Model.Should().Be("Hilux Sport");
        vehicle.CurrentMileage.Should().Be(15000);
    }

    [Fact]
    public async Task Delete_ComoConductor_DebeRetornar403()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.DeleteAsync($"/api/vehicles/{_factory.VehicleTenantAId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_VehiculoInexistente_DebeRetornar404()
    {
        var client = CreateAuthenticatedClient(_factory.AdminTenantAId);

        var response = await client.DeleteAsync($"/api/vehicles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReportMileage_ComoConductorAsignado_DebeRetornar200()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.PostAsJsonAsync($"/api/vehicles/{_factory.VehicleTenantAId}/report-mileage", new { newMileage = 99999 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicle = await response.Content.ReadFromJsonAsync<VehicleUpdateResponse>();
        vehicle!.CurrentMileage.Should().Be(99999);
    }

    [Fact]
    public async Task ReportMileage_ConKilometrajeMenorAlActual_DebeRetornar400()
    {
        var client = CreateAuthenticatedClient(_factory.DriverTenantAId);

        var response = await client.PostAsJsonAsync($"/api/vehicles/{_factory.VehicleTenantAId}/report-mileage", new { newMileage = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_DebeRetornar200_SinAutenticacion()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record VehicleResponse(Guid Id, string LicensePlate);
    private record VehicleUpdateResponse(Guid Id, string Model, int CurrentMileage);
}
