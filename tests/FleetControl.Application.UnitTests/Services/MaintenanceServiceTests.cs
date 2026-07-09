using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
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
/// Pruebas de MaintenanceService: registro de mantenimientos (solo admin),
/// actualizacion condicional del odometro del vehiculo, y consulta del
/// estado de semaforo por vehiculo con reglas de autorizacion por rol.
/// </summary>
public class MaintenanceServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public MaintenanceServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
    }

    private MaintenanceService CreateSut() =>
        new(_context, _currentUserMock.Object, new MaintenanceAlertCalculator());

    private Vehicle AddVehicle(int currentMileage = 1000, Guid? assignedDriverId = null)
    {
        var vehicle = new Vehicle
        {
            TenantId = _tenantId,
            LicensePlate = "AAA-111",
            Brand = "Toyota",
            Model = "Hilux",
            ManufactureYear = 2022,
            CurrentMileage = currentMileage,
            AssignedDriverId = assignedDriverId
        };
        _context.Vehicles.Add(vehicle);
        _context.SaveChanges();
        return vehicle;
    }

    private MaintenanceType AddMaintenanceType(int intervalKm = 5000)
    {
        var type = new MaintenanceType
        {
            Code = MaintenanceTypeCode.OilChange,
            Name = "Cambio de aceite",
            IntervalKm = intervalKm,
            EstimatedCost = 100
        };
        _context.MaintenanceTypes.Add(type);
        _context.SaveChanges();
        return type;
    }

    [Fact]
    public async Task RegisterMaintenanceAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();
        var dto = new CreateMaintenanceLogDto(Guid.NewGuid(), Guid.NewGuid(), 1000, new DateOnly(2026, 1, 1), 100, null);

        var act = async () => await sut.RegisterMaintenanceAsync(dto);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task RegisterMaintenanceAsync_DebeLanzarNotFound_SiVehiculoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();
        var dto = new CreateMaintenanceLogDto(Guid.NewGuid(), Guid.NewGuid(), 1000, new DateOnly(2026, 1, 1), 100, null);

        var act = async () => await sut.RegisterMaintenanceAsync(dto);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RegisterMaintenanceAsync_DebeLanzarNotFound_SiTipoDeMantenimientoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var sut = CreateSut();
        var dto = new CreateMaintenanceLogDto(vehicle.Id, Guid.NewGuid(), 1000, new DateOnly(2026, 1, 1), 100, null);

        var act = async () => await sut.RegisterMaintenanceAsync(dto);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RegisterMaintenanceAsync_DebeActualizarOdometro_CuandoKilometrajeDeServicioEsMayorAlActual()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        var vehicle = AddVehicle(currentMileage: 1000);
        var type = AddMaintenanceType();
        var sut = CreateSut();
        var dto = new CreateMaintenanceLogDto(vehicle.Id, type.Id, 1500, new DateOnly(2026, 1, 1), 120, "cambio de rutina");

        var result = await sut.RegisterMaintenanceAsync(dto);

        result.MaintenanceTypeName.Should().Be("Cambio de aceite");
        result.Cost.Should().Be(120);
        vehicle.CurrentMileage.Should().Be(1500);
        (await _context.MaintenanceLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RegisterMaintenanceAsync_NoDebeActualizarOdometro_CuandoKilometrajeDeServicioEsMenorAlActual()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle(currentMileage: 5000);
        var type = AddMaintenanceType();
        var sut = CreateSut();
        // Registro tardio de un mantenimiento pasado, con km menor al actual del vehiculo.
        var dto = new CreateMaintenanceLogDto(vehicle.Id, type.Id, 3000, new DateOnly(2025, 6, 1), 90, null);

        await sut.RegisterMaintenanceAsync(dto);

        vehicle.CurrentMileage.Should().Be(5000); // no retrocede
    }

    [Fact]
    public async Task GetVehicleMaintenanceStatusAsync_DebeLanzarNotFound_SiVehiculoNoExiste()
    {
        var sut = CreateSut();

        var act = async () => await sut.GetVehicleMaintenanceStatusAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetVehicleMaintenanceStatusAsync_DebeLanzarForbidden_SiConductorNoEsElAsignado()
    {
        var otroConductor = Guid.NewGuid();
        var vehicle = AddVehicle(assignedDriverId: otroConductor);
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        var sut = CreateSut();

        var act = async () => await sut.GetVehicleMaintenanceStatusAsync(vehicle.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task GetVehicleMaintenanceStatusAsync_DebePermitirAlConductorAsignado()
    {
        var driverId = Guid.NewGuid();
        var vehicle = AddVehicle(currentMileage: 2000, assignedDriverId: driverId);
        AddMaintenanceType(intervalKm: 5000);
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.UserId).Returns(driverId);
        var sut = CreateSut();

        var result = await sut.GetVehicleMaintenanceStatusAsync(vehicle.Id);

        result.Should().ContainSingle();
        result[0].Status.Should().Be(AlertStatus.Green);
    }

    [Fact]
    public async Task GetVehicleMaintenanceStatusAsync_DebeUsarElUltimoLogConMayorKilometraje()
    {
        var vehicle = AddVehicle(currentMileage: 12000);
        var type = AddMaintenanceType(intervalKm: 5000);
        _context.MaintenanceLogs.AddRange(
            new MaintenanceLog { TenantId = _tenantId, VehicleId = vehicle.Id, MaintenanceTypeId = type.Id, MileageAtService = 5000, ServiceDate = new DateOnly(2025, 1, 1) },
            new MaintenanceLog { TenantId = _tenantId, VehicleId = vehicle.Id, MaintenanceTypeId = type.Id, MileageAtService = 10000, ServiceDate = new DateOnly(2025, 6, 1) });
        _context.SaveChanges();
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var result = await sut.GetVehicleMaintenanceStatusAsync(vehicle.Id);

        result.Should().ContainSingle();
        result[0].LastServiceMileage.Should().Be(10000);
        result[0].WearPercentage.Should().Be(40); // (12000-10000)/5000
    }

    [Fact]
    public async Task GetVehicleMaintenanceHistoryAsync_DebeLanzarNotFound_SiVehiculoNoExiste()
    {
        var sut = CreateSut();

        var act = async () => await sut.GetVehicleMaintenanceHistoryAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetVehicleMaintenanceHistoryAsync_DebeLanzarForbidden_SiConductorNoEsElAsignado()
    {
        var vehicle = AddVehicle(assignedDriverId: Guid.NewGuid());
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        var sut = CreateSut();

        var act = async () => await sut.GetVehicleMaintenanceHistoryAsync(vehicle.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task GetVehicleMaintenanceHistoryAsync_DebeRetornarTodosLosLogs_OrdenadosPorFechaDescendente()
    {
        var vehicle = AddVehicle(currentMileage: 12000);
        var type = AddMaintenanceType(intervalKm: 5000);
        _context.MaintenanceLogs.AddRange(
            new MaintenanceLog { TenantId = _tenantId, VehicleId = vehicle.Id, MaintenanceTypeId = type.Id, MileageAtService = 5000, ServiceDate = new DateOnly(2025, 1, 1), Cost = 80 },
            new MaintenanceLog { TenantId = _tenantId, VehicleId = vehicle.Id, MaintenanceTypeId = type.Id, MileageAtService = 10000, ServiceDate = new DateOnly(2025, 6, 1), Cost = 90 });
        _context.SaveChanges();
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var result = await sut.GetVehicleMaintenanceHistoryAsync(vehicle.Id);

        result.Should().HaveCount(2);
        result[0].ServiceDate.Should().Be(new DateOnly(2025, 6, 1));
        result[0].MaintenanceTypeName.Should().Be("Cambio de aceite");
        result[1].ServiceDate.Should().Be(new DateOnly(2025, 1, 1));
    }

    public void Dispose() => _context.Dispose();
}
