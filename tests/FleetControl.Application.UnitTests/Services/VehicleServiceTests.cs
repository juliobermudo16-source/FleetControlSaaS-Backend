using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
using FleetControl.Application.Services;
using FleetControl.Domain.Enums;
using FleetControl.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FleetControl.Application.UnitTests.Services;

/// <summary>
/// Pruebas de VehicleService usando Moq para simular ICurrentUserService
/// (identidad/rol del usuario) y una base de datos EF Core InMemory como
/// implementacion de IApplicationDbContext, para verificar las reglas de
/// autorizacion por rol sin necesitar una base de datos real.
/// </summary>
public class VehicleServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public VehicleServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options); // sin ICurrentUserService -> sin query filters, ideal para setup de test
        _dateTimeMock.SetupGet(d => d.Today).Returns(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private VehicleService CreateSut() =>
        new(_context, _currentUserMock.Object, new MaintenanceAlertCalculator(), _dateTimeMock.Object);

    [Fact]
    public async Task CreateAsync_DebeLanzarForbidden_CuandoElUsuarioEsConductor()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);

        var sut = CreateSut();
        var dto = new CreateVehicleDto("ABC-123", "Toyota", "Hilux", 2022, "Blanco", 0, null);

        var act = async () => await sut.CreateAsync(dto);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task CreateAsync_DebeCrearVehiculo_CuandoElUsuarioEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);

        var sut = CreateSut();
        var dto = new CreateVehicleDto("abc-123", "Toyota", "Hilux", 2022, "Blanco", 15000, null);

        var result = await sut.CreateAsync(dto);

        result.LicensePlate.Should().Be("ABC-123"); // se normaliza a mayusculas
        result.CurrentMileage.Should().Be(15000);
        (await _context.Vehicles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReportMileageAsync_DebeLanzarExcepcion_SiElNuevoKilometrajeEsMenorAlActual()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sut = CreateSut();
        var vehicle = await sut.CreateAsync(new CreateVehicleDto("XYZ-789", "Kia", "Rio", 2021, "Rojo", 20000, null));

        var act = async () => await sut.ReportMileageAsync(vehicle.Id, new ReportMileageDto(19000));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReportMileageAsync_DebeLanzarForbidden_SiElConductorNoEsElAsignado()
    {
        var adminId = Guid.NewGuid();
        var otroConductorId = Guid.NewGuid();

        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sutAdmin = CreateSut();
        var vehicle = await sutAdmin.CreateAsync(new CreateVehicleDto("DEF-456", "Nissan", "Frontier", 2020, "Gris", 30000, otroConductorId));

        // Ahora simulamos que un conductor DISTINTO al asignado intenta reportar kilometraje.
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid()); // otro conductor
        var sutDriver = CreateSut();

        var act = async () => await sutDriver.ReportMileageAsync(vehicle.Id, new ReportMileageDto(31000));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task GetVehiclesAsync_ComoAdmin_DebeRetornarTodosLosVehiculosDelTenant()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sut = CreateSut();
        await sut.CreateAsync(new CreateVehicleDto("AAA-111", "Toyota", "Hilux", 2022, "Blanco", 0, null));
        await sut.CreateAsync(new CreateVehicleDto("BBB-222", "Kia", "Rio", 2021, "Rojo", 0, null));

        var result = await sut.GetVehiclesAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetVehiclesAsync_ComoConductor_SoloDebeRetornarSuVehiculoAsignado()
    {
        var driverId = Guid.NewGuid();
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sutAdmin = CreateSut();
        await sutAdmin.CreateAsync(new CreateVehicleDto("AAA-111", "Toyota", "Hilux", 2022, "Blanco", 0, driverId));
        await sutAdmin.CreateAsync(new CreateVehicleDto("BBB-222", "Kia", "Rio", 2021, "Rojo", 0, null));

        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.UserId).Returns(driverId);
        var sutDriver = CreateSut();

        var result = await sutDriver.GetVehiclesAsync();

        result.Should().ContainSingle(v => v.LicensePlate == "AAA-111");
    }

    [Fact]
    public async Task GetByIdAsync_DebeLanzarNotFound_SiElVehiculoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var act = async () => await sut.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_DebeLanzarForbidden_SiElConductorNoEsElAsignado()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sutAdmin = CreateSut();
        var vehicle = await sutAdmin.CreateAsync(new CreateVehicleDto("AAA-111", "Toyota", "Hilux", 2022, "Blanco", 0, null));

        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        var sutDriver = CreateSut();

        var act = async () => await sutDriver.GetByIdAsync(vehicle.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UpdateAsync_DebeLanzarForbidden_CuandoElUsuarioEsConductor()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();
        var dto = new UpdateVehicleDto("Toyota", "Hilux", "Negro", 5000, null, VehicleStatus.Active);

        var act = async () => await sut.UpdateAsync(Guid.NewGuid(), dto);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UpdateAsync_DebeActualizarLosCamposDelVehiculo_CuandoElUsuarioEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sut = CreateSut();
        var created = await sut.CreateAsync(new CreateVehicleDto("AAA-111", "Toyota", "Hilux", 2022, "Blanco", 0, null));

        var newDriverId = Guid.NewGuid();
        var dto = new UpdateVehicleDto("Toyota", "Hilux Sport", "Negro", 8000, newDriverId, VehicleStatus.Maintenance);
        var result = await sut.UpdateAsync(created.Id, dto);

        result.Model.Should().Be("Hilux Sport");
        result.Color.Should().Be("Negro");
        result.CurrentMileage.Should().Be(8000);
        result.Status.Should().Be(VehicleStatus.Maintenance);
    }

    [Fact]
    public async Task UpdateAsync_DebeLanzarNotFound_SiElVehiculoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();
        var dto = new UpdateVehicleDto("Toyota", "Hilux", "Negro", 5000, null, VehicleStatus.Active);

        var act = async () => await sut.UpdateAsync(Guid.NewGuid(), dto);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_DebeLanzarForbidden_CuandoElUsuarioEsConductor()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();

        var act = async () => await sut.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task DeleteAsync_DebeEliminarElVehiculo_CuandoElUsuarioEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sut = CreateSut();
        var created = await sut.CreateAsync(new CreateVehicleDto("AAA-111", "Toyota", "Hilux", 2022, "Blanco", 0, null));

        await sut.DeleteAsync(created.Id);

        (await _context.Vehicles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReportMileageAsync_DebeActualizarKilometraje_CuandoElConductorAsignadoReporta()
    {
        var driverId = Guid.NewGuid();
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var sutAdmin = CreateSut();
        var vehicle = await sutAdmin.CreateAsync(new CreateVehicleDto("AAA-111", "Toyota", "Hilux", 2022, "Blanco", 1000, driverId));

        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        _currentUserMock.SetupGet(u => u.UserId).Returns(driverId);
        var sutDriver = CreateSut();

        var result = await sutDriver.ReportMileageAsync(vehicle.Id, new ReportMileageDto(1500));

        result.CurrentMileage.Should().Be(1500);
    }

    [Fact]
    public async Task ReportMileageAsync_DebeLanzarNotFound_SiElVehiculoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var act = async () => await sut.ReportMileageAsync(Guid.NewGuid(), new ReportMileageDto(1000));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    public void Dispose() => _context.Dispose();
}
