using System.Text;
using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
using FleetControl.Application.Services;
using FleetControl.Domain.Entities;
using FleetControl.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FleetControl.Application.UnitTests.Services;

/// <summary>
/// Pruebas de VehiclePhotoService: autorizacion (solo admin sube/elimina),
/// marcado de foto principal (unica por vehiculo), y listado ordenado.
/// </summary>
public class VehiclePhotoServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ISupabaseStorageService> _storageMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public VehiclePhotoServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        _storageMock.Setup(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string bucket, string path) => $"https://fake.test/{bucket}/{path}");
    }

    private VehiclePhotoService CreateSut() => new(_context, _currentUserMock.Object, _storageMock.Object);

    private Vehicle AddVehicle()
    {
        var vehicle = new Vehicle { TenantId = _tenantId, LicensePlate = "AAA-111", Brand = "Toyota", Model = "Hilux", ManufactureYear = 2022 };
        _context.Vehicles.Add(vehicle);
        _context.SaveChanges();
        return vehicle;
    }

    private static MemoryStream MakeFile() => new(Encoding.UTF8.GetBytes("fake-image-bytes"));

    [Fact]
    public async Task UploadAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();

        var act = async () => await sut.UploadAsync(new UploadPhotoDto(Guid.NewGuid(), false), MakeFile(), "foto.jpg");

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UploadAsync_DebeLanzarNotFound_SiVehiculoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var act = async () => await sut.UploadAsync(new UploadPhotoDto(Guid.NewGuid(), false), MakeFile(), "foto.jpg");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UploadAsync_DebeSubirYPersistir_YRetornarUrlPublica()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var sut = CreateSut();

        var result = await sut.UploadAsync(new UploadPhotoDto(vehicle.Id, true), MakeFile(), "foto.jpg");

        result.VehicleId.Should().Be(vehicle.Id);
        result.IsPrimary.Should().BeTrue();
        result.Url.Should().Contain("vehicle-photos");
        (await _context.VehiclePhotos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UploadAsync_DebeDesmarcarLaFotoPrincipalAnterior_AlSubirUnaNuevaPrincipal()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var sut = CreateSut();

        var first = await sut.UploadAsync(new UploadPhotoDto(vehicle.Id, true), MakeFile(), "foto1.jpg");
        var second = await sut.UploadAsync(new UploadPhotoDto(vehicle.Id, true), MakeFile(), "foto2.jpg");

        var photos = await _context.VehiclePhotos.ToListAsync();
        photos.Single(p => p.Id == first.Id).IsPrimary.Should().BeFalse();
        photos.Single(p => p.Id == second.Id).IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task GetByVehicleAsync_DebeRetornarSoloLasFotosDelVehiculo_ConLaPrincipalPrimero()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var sut = CreateSut();
        await sut.UploadAsync(new UploadPhotoDto(vehicle.Id, false), MakeFile(), "a.jpg");
        var primary = await sut.UploadAsync(new UploadPhotoDto(vehicle.Id, true), MakeFile(), "b.jpg");

        var result = await sut.GetByVehicleAsync(vehicle.Id);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(primary.Id);
    }

    [Fact]
    public async Task DeleteAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();

        var act = async () => await sut.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task DeleteAsync_DebeLanzarNotFound_SiLaFotoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var act = async () => await sut.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_DebeEliminarLaFoto_YBorrarlaDelStorage()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var sut = CreateSut();
        var photo = await sut.UploadAsync(new UploadPhotoDto(vehicle.Id, false), MakeFile(), "a.jpg");

        await sut.DeleteAsync(photo.Id);

        (await _context.VehiclePhotos.CountAsync()).Should().Be(0);
        _storageMock.Verify(s => s.DeleteAsync("vehicle-photos", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => _context.Dispose();
}
