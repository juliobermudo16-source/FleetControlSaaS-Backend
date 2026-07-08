using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.Services;
using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using FleetControl.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FleetControl.Application.UnitTests.Services;

/// <summary>
/// Pruebas de DashboardService: agregacion de KPIs (verde/amarillo/rojo),
/// costo estimado de mantenimientos proximos y lista de vehiculos urgentes.
/// </summary>
public class DashboardServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateOnly _today = new(2026, 1, 1);

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        _dateTimeMock.SetupGet(d => d.Today).Returns(_today);
    }

    private DashboardService CreateSut() =>
        new(_context, _currentUserMock.Object, new MaintenanceAlertCalculator(), _dateTimeMock.Object);

    private MaintenanceType AddOilChangeType(int intervalKm = 5000, decimal estimatedCost = 150, Guid? tenantId = null)
    {
        var type = new MaintenanceType
        {
            TenantId = tenantId,
            Code = MaintenanceTypeCode.OilChange,
            Name = "Cambio de aceite",
            IntervalKm = intervalKm,
            EstimatedCost = estimatedCost
        };
        _context.MaintenanceTypes.Add(type);
        _context.SaveChanges();
        return type;
    }

    private Vehicle AddVehicle(int currentMileage, string plate = "AAA-111")
    {
        var vehicle = new Vehicle
        {
            TenantId = _tenantId,
            LicensePlate = plate,
            Brand = "Toyota",
            Model = "Hilux",
            ManufactureYear = 2022,
            CurrentMileage = currentMileage
        };
        _context.Vehicles.Add(vehicle);
        _context.SaveChanges();
        return vehicle;
    }

    [Fact]
    public async Task GetSummaryAsync_DebeRetornarCerosVacio_CuandoNoHayVehiculos()
    {
        var sut = CreateSut();

        var result = await sut.GetSummaryAsync();

        result.TotalVehicles.Should().Be(0);
        result.GreenCount.Should().Be(0);
        result.YellowCount.Should().Be(0);
        result.RedCount.Should().Be(0);
        result.EstimatedUpcomingCost.Should().Be(0);
        result.UrgentVehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummaryAsync_DebeContarVehiculoComoVerde_CuandoDesgasteEsBajo()
    {
        AddOilChangeType(intervalKm: 5000);
        AddVehicle(currentMileage: 1000); // 20% de desgaste -> verde

        var sut = CreateSut();
        var result = await sut.GetSummaryAsync();

        result.TotalVehicles.Should().Be(1);
        result.GreenCount.Should().Be(1);
        result.YellowCount.Should().Be(0);
        result.RedCount.Should().Be(0);
        result.UrgentVehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummaryAsync_DebeContarVehiculoComoRojo_YSumarCostoEstimado_CuandoDesgasteSuperaElLimite()
    {
        AddOilChangeType(intervalKm: 5000, estimatedCost: 200);
        var vehicle = AddVehicle(currentMileage: 6000); // 120% -> rojo

        var sut = CreateSut();
        var result = await sut.GetSummaryAsync();

        result.RedCount.Should().Be(1);
        result.GreenCount.Should().Be(0);
        result.EstimatedUpcomingCost.Should().Be(200);
        result.UrgentVehicles.Should().ContainSingle(v => v.VehicleId == vehicle.Id);
    }

    [Fact]
    public async Task GetSummaryAsync_DebeUsarElPeorEstado_EntreMantenimientoYDocumentos()
    {
        AddOilChangeType(intervalKm: 5000);
        var vehicle = AddVehicle(currentMileage: 1000); // mantenimiento verde

        _context.Documents.Add(new VehicleDocument
        {
            TenantId = _tenantId,
            VehicleId = vehicle.Id,
            DocumentType = DocumentType.Soat,
            ExpirationDate = _today.AddDays(-5) // vencido -> rojo
        });
        _context.SaveChanges();

        var sut = CreateSut();
        var result = await sut.GetSummaryAsync();

        result.RedCount.Should().Be(1);
        result.UrgentVehicles.Should().ContainSingle(v => v.VehicleId == vehicle.Id && v.OverallStatus == AlertStatus.Red);
    }

    [Fact]
    public async Task GetSummaryAsync_NoDebeIncluirTiposDeMantenimientoDeOtroTenant()
    {
        var otroTenantId = Guid.NewGuid();
        AddOilChangeType(intervalKm: 100, estimatedCost: 999, tenantId: otroTenantId); // no deberia aplicar
        AddVehicle(currentMileage: 10000); // sin tipos de mantenimiento propios -> ningun item

        var sut = CreateSut();
        var result = await sut.GetSummaryAsync();

        result.GreenCount.Should().Be(1); // sin items de mantenimiento => worst permanece Verde
        result.EstimatedUpcomingCost.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_DebeUsarElUltimoKilometrajeDeServicio_ConMayorKilometraje()
    {
        var type = AddOilChangeType(intervalKm: 5000);
        var vehicle = AddVehicle(currentMileage: 12000);

        _context.MaintenanceLogs.AddRange(
            new MaintenanceLog { TenantId = _tenantId, VehicleId = vehicle.Id, MaintenanceTypeId = type.Id, MileageAtService = 8000, ServiceDate = _today },
            new MaintenanceLog { TenantId = _tenantId, VehicleId = vehicle.Id, MaintenanceTypeId = type.Id, MileageAtService = 10000, ServiceDate = _today });
        _context.SaveChanges();

        var sut = CreateSut();
        var result = await sut.GetSummaryAsync();

        // 12000 - 10000 = 2000 / 5000 = 40% -> verde
        result.GreenCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSummaryAsync_UrgentVehicles_DebeOrdenarsePorEstadoDescendente()
    {
        var type = AddOilChangeType(intervalKm: 5000, estimatedCost: 100);
        var vehicleYellow = AddVehicle(currentMileage: 4500, plate: "YEL-001"); // 90% -> amarillo
        var vehicleRed = AddVehicle(currentMileage: 6000, plate: "RED-002");    // 120% -> rojo
        _ = type;

        var sut = CreateSut();
        var result = await sut.GetSummaryAsync();

        result.UrgentVehicles.Should().HaveCount(2);
        result.UrgentVehicles[0].VehicleId.Should().Be(vehicleRed.Id);
        result.UrgentVehicles[1].VehicleId.Should().Be(vehicleYellow.Id);
    }

    public void Dispose() => _context.Dispose();
}
