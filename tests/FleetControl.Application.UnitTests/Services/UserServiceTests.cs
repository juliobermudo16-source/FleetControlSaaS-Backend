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
/// Pruebas de UserService: perfil del usuario actual, listado de usuarios
/// del tenant (solo Admin) e invitacion de usuarios nuevos (crea el usuario
/// en Supabase Auth via ISupabaseAuthAdminService y el perfil en public.users).
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ISupabaseAuthAdminService> _authAdminMock = new();
    private readonly Mock<ISupabaseStorageService> _storageMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        _dateTimeMock.SetupGet(d => d.UtcNow).Returns(_now);
        _storageMock.Setup(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string bucket, string path) => $"https://storage.test/{bucket}/{path}");
    }

    private UserService CreateSut() => new(_context, _currentUserMock.Object, _authAdminMock.Object, _storageMock.Object, _dateTimeMock.Object);

    private AppUser AddUser(string fullName, string email, UserRole role = UserRole.Driver, bool isActive = true)
    {
        var user = new AppUser { TenantId = _tenantId, FullName = fullName, Email = email, Role = role, IsActive = isActive };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private Vehicle AddVehicle(string licensePlate, Guid? assignedDriverId)
    {
        var vehicle = new Vehicle
        {
            TenantId = _tenantId,
            LicensePlate = licensePlate,
            Brand = "Toyota",
            Model = "Hilux",
            ManufactureYear = 2022,
            AssignedDriverId = assignedDriverId
        };
        _context.Vehicles.Add(vehicle);
        _context.SaveChanges();
        return vehicle;
    }

    [Fact]
    public async Task GetCurrentUserAsync_DebeRetornarElUsuarioActual()
    {
        var user = AddUser("Julio Bermudo", "julio@test.com", UserRole.Admin);
        _currentUserMock.SetupGet(u => u.UserId).Returns(user.Id);
        var sut = CreateSut();

        var result = await sut.GetCurrentUserAsync();

        result.Id.Should().Be(user.Id);
        result.FullName.Should().Be("Julio Bermudo");
        result.Role.Should().Be("admin");
    }

    [Fact]
    public async Task GetCurrentUserAsync_DebeLanzarNotFound_SiElUsuarioNoExiste()
    {
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        var sut = CreateSut();

        var act = async () => await sut.GetCurrentUserAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetTenantUsersAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();

        var act = async () => await sut.GetTenantUsersAsync();

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task GetTenantUsersAsync_DebeRetornarUsuariosOrdenadosPorNombre()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        AddUser("Zoila Torres", "zoila@test.com");
        AddUser("Ana Ramos", "ana@test.com");
        var sut = CreateSut();

        var result = await sut.GetTenantUsersAsync();

        result.Should().HaveCount(2);
        result[0].FullName.Should().Be("Ana Ramos");
        result[1].FullName.Should().Be("Zoila Torres");
    }

    [Fact]
    public async Task InviteUserAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();
        var dto = new InviteUserDto("Conductor Nuevo", "nuevo@test.com", "driver", null);

        var act = async () => await sut.InviteUserAsync(dto);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _authAdminMock.Verify(a => a.InviteUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData("")]
    [InlineData("Admin")]
    public async Task InviteUserAsync_DebeLanzarExcepcion_SiElRolNoEsValido(string invalidRole)
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();
        var dto = new InviteUserDto("Alguien", "alguien@test.com", invalidRole, null);

        var act = async () => await sut.InviteUserAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InviteUserAsync_DebeLanzarExcepcion_SiYaExisteElCorreoEnElTenant()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        AddUser("Existente", "duplicado@test.com");
        var sut = CreateSut();
        var dto = new InviteUserDto("Otro", "duplicado@test.com", "driver", null);

        var act = async () => await sut.InviteUserAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _authAdminMock.Verify(a => a.InviteUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InviteUserAsync_DebeCrearElUsuario_ConElIdDevueltoPorSupabase()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        var newUserId = Guid.NewGuid();
        _authAdminMock.Setup(a => a.InviteUserByEmailAsync("conductor@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(newUserId);

        var sut = CreateSut();
        var dto = new InviteUserDto("Conductor Nuevo", "conductor@test.com", "driver", "987654321");

        var result = await sut.InviteUserAsync(dto);

        result.Id.Should().Be(newUserId);
        result.Role.Should().Be("driver");
        result.Phone.Should().Be("987654321");

        var persisted = await _context.Users.FirstAsync(u => u.Id == newUserId);
        persisted.TenantId.Should().Be(_tenantId);
        persisted.Role.Should().Be(UserRole.Driver);
        persisted.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task InviteUserAsync_DebeCrearElUsuario_ConRolAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _authAdminMock.Setup(a => a.InviteUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());

        var sut = CreateSut();
        var dto = new InviteUserDto("Otro Admin", "otroadmin@test.com", "admin", null);

        var result = await sut.InviteUserAsync(dto);

        result.Role.Should().Be("admin");
    }

    [Fact]
    public async Task DeactivateUserAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        var driver = AddUser("Conductor Uno", "conductor@test.com");
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();

        var act = async () => await sut.DeactivateUserAsync(driver.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task DeactivateUserAsync_DebeLanzarExcepcion_SiElAdminSeQuiereEliminarASiMismo()
    {
        var admin = AddUser("Admin", "admin@test.com", UserRole.Admin);
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.UserId).Returns(admin.Id);
        var sut = CreateSut();

        var act = async () => await sut.DeactivateUserAsync(admin.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeactivateUserAsync_DebeLanzarNotFound_SiElUsuarioNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        var sut = CreateSut();

        var act = async () => await sut.DeactivateUserAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeactivateUserAsync_DebeDesactivarAlUsuario_YDesasignarSusVehiculos()
    {
        var admin = AddUser("Admin", "admin@test.com", UserRole.Admin);
        var driver = AddUser("Conductor Uno", "conductor@test.com");
        var vehicle = AddVehicle("ABC-123", driver.Id);
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.UserId).Returns(admin.Id);
        var sut = CreateSut();

        await sut.DeactivateUserAsync(driver.Id);

        var persisted = await _context.Users.FirstAsync(u => u.Id == driver.Id);
        persisted.IsActive.Should().BeFalse();

        var persistedVehicle = await _context.Vehicles.FirstAsync(v => v.Id == vehicle.Id);
        persistedVehicle.AssignedDriverId.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateUserAsync_DebeProgramarElBorradoPermanente_A10MinutosDeDistancia()
    {
        var admin = AddUser("Admin", "admin@test.com", UserRole.Admin);
        var driver = AddUser("Conductor Uno", "conductor@test.com");
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        _currentUserMock.SetupGet(u => u.UserId).Returns(admin.Id);
        var sut = CreateSut();

        await sut.DeactivateUserAsync(driver.Id);

        var persisted = await _context.Users.FirstAsync(u => u.Id == driver.Id);
        persisted.PendingDeletionAt.Should().Be(_now.AddMinutes(10));
    }

    [Fact]
    public async Task ReactivateUserAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        var driver = AddUser("Conductor Uno", "conductor@test.com", isActive: false);
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();

        var act = async () => await sut.ReactivateUserAsync(driver.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ReactivateUserAsync_DebeReactivarAlUsuario_YCancelarElBorradoProgramado()
    {
        var driver = AddUser("Conductor Uno", "conductor@test.com", isActive: false);
        driver.PendingDeletionAt = _now.AddMinutes(5);
        await _context.SaveChangesAsync();
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        await sut.ReactivateUserAsync(driver.Id);

        var persisted = await _context.Users.FirstAsync(u => u.Id == driver.Id);
        persisted.IsActive.Should().BeTrue();
        persisted.PendingDeletionAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMyProfileAsync_DebeActualizarNombreYTelefono()
    {
        var user = AddUser("Nombre Viejo", "user@test.com");
        _currentUserMock.SetupGet(u => u.UserId).Returns(user.Id);
        var sut = CreateSut();

        var result = await sut.UpdateMyProfileAsync(new UpdateProfileDto("Nombre Nuevo", "999888777"));

        result.FullName.Should().Be("Nombre Nuevo");
        result.Phone.Should().Be("999888777");
        var persisted = await _context.Users.FirstAsync(u => u.Id == user.Id);
        persisted.FullName.Should().Be("Nombre Nuevo");
        persisted.Phone.Should().Be("999888777");
    }

    [Fact]
    public async Task UpdateMyProfileAsync_DebeLanzarExcepcion_SiElNombreEstaVacio()
    {
        var user = AddUser("Nombre Viejo", "user@test.com");
        _currentUserMock.SetupGet(u => u.UserId).Returns(user.Id);
        var sut = CreateSut();

        var act = async () => await sut.UpdateMyProfileAsync(new UpdateProfileDto("   ", null));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UploadMyAvatarAsync_DebeGuardarLaRutaYDevolverLaUrlPublica()
    {
        var user = AddUser("Julio", "julio@test.com");
        _currentUserMock.SetupGet(u => u.UserId).Returns(user.Id);
        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await sut.UploadMyAvatarAsync(stream, "foto.jpg");

        result.AvatarUrl.Should().StartWith("https://storage.test/user-avatars/");
        var persisted = await _context.Users.FirstAsync(u => u.Id == user.Id);
        persisted.AvatarStoragePath.Should().NotBeNullOrEmpty();
        _storageMock.Verify(s => s.UploadAsync("user-avatars", It.IsAny<string>(), stream, "image/jpeg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadMyAvatarAsync_DebeBorrarLaFotoAnterior_SiYaTeniaUna()
    {
        var user = AddUser("Julio", "julio@test.com");
        user.AvatarStoragePath = "tenant/user/vieja.jpg";
        await _context.SaveChangesAsync();
        _currentUserMock.SetupGet(u => u.UserId).Returns(user.Id);
        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[] { 1 });

        await sut.UploadMyAvatarAsync(stream, "nueva.jpg");

        _storageMock.Verify(s => s.DeleteAsync("user-avatars", "tenant/user/vieja.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => _context.Dispose();
}
