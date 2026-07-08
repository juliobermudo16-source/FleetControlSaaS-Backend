using System.Reflection;
using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.Services;
using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using FleetControl.Infrastructure.BackgroundServices;
using FleetControl.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FleetControl.Application.UnitTests.Infrastructure;

/// <summary>
/// Pruebas del corazon de MaintenanceAlertBackgroundService: la evaluacion de
/// alertas y el envio de notificaciones. El bucle publico (ExecuteAsync) corre
/// cada 12 horas y no es practico de testear directamente, asi que se invoca
/// por reflexion el metodo privado EvaluateAndNotifyAsync, que es donde vive
/// toda la logica de negocio real.
/// </summary>
public class MaintenanceAlertBackgroundServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateOnly _today = new(2026, 1, 1);
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

    public MaintenanceAlertBackgroundServiceTests()
    {
        _dateTimeMock.SetupGet(d => d.Today).Returns(_today);
        _emailMock
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dbName = Guid.NewGuid().ToString();
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;

        // El propio servicio resuelve su ApplicationDbContext via
        // IServiceScopeFactory.CreateScope(), asi que se registra en un
        // contenedor DI real apuntando a la MISMA base InMemory que se usa
        // para sembrar/verificar datos directamente (sin pasar por DI, para
        // no toparse con el cacheo de instancias "scoped" del root provider).
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<IMaintenanceAlertCalculator, MaintenanceAlertCalculator>();
        services.AddSingleton(_emailMock.Object);
        services.AddSingleton(_dateTimeMock.Object);
        _provider = services.BuildServiceProvider();
    }

    private MaintenanceAlertBackgroundService CreateSut() =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MaintenanceAlertBackgroundService>.Instance);

    private static Task InvokeEvaluateAndNotifyAsync(MaintenanceAlertBackgroundService sut)
    {
        var method = typeof(MaintenanceAlertBackgroundService).GetMethod("EvaluateAndNotifyAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(sut, new object[] { CancellationToken.None })!;
    }

    private ApplicationDbContext NewDbContext() => new(_dbOptions);

    [Fact]
    public async Task EvaluateAndNotifyAsync_DebeEnviarCorreoAlAdminYAlConductor_CuandoHayAlertaRoja()
    {
        var adminId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.AddRange(
                new AppUser { Id = adminId, TenantId = _tenantId, FullName = "Admin", Email = "admin@test.com", Role = UserRole.Admin },
                new AppUser { Id = driverId, TenantId = _tenantId, FullName = "Conductor", Email = "conductor@test.com", Role = UserRole.Driver });
            db.MaintenanceTypes.Add(new MaintenanceType { Id = Guid.NewGuid(), Code = MaintenanceTypeCode.OilChange, Name = "Cambio de aceite", IntervalKm = 5000, EstimatedCost = 100 });
            db.Vehicles.Add(new Vehicle { TenantId = _tenantId, LicensePlate = "AAA-111", Brand = "Toyota", Model = "Hilux", ManufactureYear = 2022, CurrentMileage = 6000, AssignedDriverId = driverId });
            db.SaveChanges();
        }

        var sut = CreateSut();
        await InvokeEvaluateAndNotifyAsync(sut);

        _emailMock.Verify(e => e.SendAsync("admin@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailMock.Verify(e => e.SendAsync("conductor@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        using var verifyDb = NewDbContext();
        (await verifyDb.NotificationLogs.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task EvaluateAndNotifyAsync_NoDebeEnviarCorreo_CuandoElVehiculoEstaEnVerde()
    {
        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.Add(new AppUser { Id = Guid.NewGuid(), TenantId = _tenantId, FullName = "Admin", Email = "admin@test.com", Role = UserRole.Admin });
            db.MaintenanceTypes.Add(new MaintenanceType { Id = Guid.NewGuid(), Code = MaintenanceTypeCode.OilChange, Name = "Cambio de aceite", IntervalKm = 5000, EstimatedCost = 100 });
            db.Vehicles.Add(new Vehicle { TenantId = _tenantId, LicensePlate = "AAA-111", Brand = "Toyota", Model = "Hilux", ManufactureYear = 2022, CurrentMileage = 500 });
            db.SaveChanges();
        }

        var sut = CreateSut();
        await InvokeEvaluateAndNotifyAsync(sut);

        _emailMock.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAndNotifyAsync_NoDebeReenviar_SiYaSeNotificoElMismoDia()
    {
        var adminId = Guid.NewGuid();
        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.Add(new AppUser { Id = adminId, TenantId = _tenantId, FullName = "Admin", Email = "admin@test.com", Role = UserRole.Admin });
            db.MaintenanceTypes.Add(new MaintenanceType { Id = Guid.NewGuid(), Code = MaintenanceTypeCode.OilChange, Name = "Cambio de aceite", IntervalKm = 5000, EstimatedCost = 100 });
            db.Vehicles.Add(new Vehicle { TenantId = _tenantId, LicensePlate = "AAA-111", Brand = "Toyota", Model = "Hilux", ManufactureYear = 2022, CurrentMileage = 6000 });
            db.SaveChanges();
        }

        var sut = CreateSut();
        await InvokeEvaluateAndNotifyAsync(sut); // primera evaluacion: envia
        await InvokeEvaluateAndNotifyAsync(sut); // segunda evaluacion, mismo dia: no reenvia

        _emailMock.Verify(e => e.SendAsync("admin@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAndNotifyAsync_DebeEvaluarDocumentosVencidos()
    {
        var adminId = Guid.NewGuid();
        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.Add(new AppUser { Id = adminId, TenantId = _tenantId, FullName = "Admin", Email = "admin@test.com", Role = UserRole.Admin });
            var vehicle = new Vehicle { TenantId = _tenantId, LicensePlate = "AAA-111", Brand = "Toyota", Model = "Hilux", ManufactureYear = 2022, CurrentMileage = 100 };
            db.Vehicles.Add(vehicle);
            db.SaveChanges();
            db.Documents.Add(new VehicleDocument { TenantId = _tenantId, VehicleId = vehicle.Id, DocumentType = DocumentType.Soat, ExpirationDate = _today.AddDays(-1), FileHashSha256 = "hash" });
            db.SaveChanges();
        }

        var sut = CreateSut();
        await InvokeEvaluateAndNotifyAsync(sut);

        _emailMock.Verify(e => e.SendAsync("admin@test.com", It.Is<string>(s => s.Contains("Soat")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => _provider.Dispose();
}
