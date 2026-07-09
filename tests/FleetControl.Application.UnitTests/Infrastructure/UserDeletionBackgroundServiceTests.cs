using System.Reflection;
using FleetControl.Application.Common.Interfaces;
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
/// Pruebas del corazon de UserDeletionBackgroundService: el bucle publico
/// (ExecuteAsync) corre cada minuto y no es practico de testear directamente,
/// asi que se invoca por reflexion el metodo privado ProcessDueDeletionsAsync.
/// </summary>
public class UserDeletionBackgroundServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly Mock<ISupabaseAuthAdminService> _authAdminMock = new();
    private readonly Mock<ISupabaseStorageService> _storageMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

    public UserDeletionBackgroundServiceTests()
    {
        _dateTimeMock.SetupGet(d => d.UtcNow).Returns(_now);
        _authAdminMock.Setup(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _storageMock.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dbName = Guid.NewGuid().ToString();
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(_authAdminMock.Object);
        services.AddSingleton(_storageMock.Object);
        services.AddSingleton(_dateTimeMock.Object);
        _provider = services.BuildServiceProvider();
    }

    private UserDeletionBackgroundService CreateSut() =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<UserDeletionBackgroundService>.Instance);

    private static Task InvokeProcessDueDeletionsAsync(UserDeletionBackgroundService sut)
    {
        var method = typeof(UserDeletionBackgroundService).GetMethod("ProcessDueDeletionsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(sut, new object[] { CancellationToken.None })!;
    }

    private ApplicationDbContext NewDbContext() => new(_dbOptions);

    [Fact]
    public async Task ProcessDueDeletionsAsync_DebeBorrarAlUsuario_CuandoElPlazoYaVencio()
    {
        var userId = Guid.NewGuid();
        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.Add(new AppUser
            {
                Id = userId, TenantId = _tenantId, FullName = "Vencido", Email = "vencido@test.com",
                Role = UserRole.Driver, IsActive = false, PendingDeletionAt = _now.AddMinutes(-1)
            });
            db.SaveChanges();
        }

        var sut = CreateSut();
        await InvokeProcessDueDeletionsAsync(sut);

        _authAdminMock.Verify(a => a.DeleteUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        using var verifyDb = NewDbContext();
        (await verifyDb.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId)).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessDueDeletionsAsync_NoDebeTocar_UsuariosCuyoPlazoAunNoVence()
    {
        var userId = Guid.NewGuid();
        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.Add(new AppUser
            {
                Id = userId, TenantId = _tenantId, FullName = "Aun no", Email = "noaun@test.com",
                Role = UserRole.Driver, IsActive = false, PendingDeletionAt = _now.AddMinutes(5)
            });
            db.SaveChanges();
        }

        var sut = CreateSut();
        await InvokeProcessDueDeletionsAsync(sut);

        _authAdminMock.Verify(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        using var verifyDb = NewDbContext();
        (await verifyDb.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId)).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessDueDeletionsAsync_DebeBorrarLaFotoDePerfil_SiTenia()
    {
        var userId = Guid.NewGuid();
        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.Add(new AppUser
            {
                Id = userId, TenantId = _tenantId, FullName = "Con foto", Email = "confoto@test.com",
                Role = UserRole.Driver, IsActive = false, PendingDeletionAt = _now.AddMinutes(-1),
                AvatarStoragePath = "tenant/user/foto.jpg"
            });
            db.SaveChanges();
        }

        var sut = CreateSut();
        await InvokeProcessDueDeletionsAsync(sut);

        _storageMock.Verify(s => s.DeleteAsync("user-avatars", "tenant/user/foto.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDueDeletionsAsync_DebeDejarPendienteParaReintento_SiFallaElBorradoEnAuth()
    {
        var userId = Guid.NewGuid();
        using (var db = NewDbContext())
        {
            db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "Empresa" });
            db.Users.Add(new AppUser
            {
                Id = userId, TenantId = _tenantId, FullName = "Falla", Email = "falla@test.com",
                Role = UserRole.Driver, IsActive = false, PendingDeletionAt = _now.AddMinutes(-1)
            });
            db.SaveChanges();
        }
        _authAdminMock.Setup(a => a.DeleteUserAsync(userId, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        var sut = CreateSut();
        await InvokeProcessDueDeletionsAsync(sut);

        using var verifyDb = NewDbContext();
        (await verifyDb.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId)).Should().BeTrue();
    }

    public void Dispose() => _provider.Dispose();
}
