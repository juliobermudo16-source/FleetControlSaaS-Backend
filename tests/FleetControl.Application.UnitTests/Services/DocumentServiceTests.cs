using System.Security.Cryptography;
using System.Text;
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
/// Pruebas de DocumentService: autorizacion (solo admin sube documentos),
/// validacion de fechas, calculo de hash SHA-256 antes de subir a Storage,
/// y generacion de URLs firmadas de descarga.
/// </summary>
public class DocumentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ISupabaseStorageService> _storageMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateOnly _today = new(2026, 1, 1);

    public DocumentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _currentUserMock.SetupGet(u => u.TenantId).Returns(_tenantId);
        _currentUserMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        _dateTimeMock.SetupGet(d => d.Today).Returns(_today);
    }

    private DocumentService CreateSut() =>
        new(_context, _currentUserMock.Object, _storageMock.Object, new MaintenanceAlertCalculator(), _dateTimeMock.Object);

    private Vehicle AddVehicle()
    {
        var vehicle = new Vehicle
        {
            TenantId = _tenantId,
            LicensePlate = "AAA-111",
            Brand = "Toyota",
            Model = "Hilux",
            ManufactureYear = 2022,
            CurrentMileage = 1000
        };
        _context.Vehicles.Add(vehicle);
        _context.SaveChanges();
        return vehicle;
    }

    private static MemoryStream MakeFile(string content = "contenido-de-prueba") =>
        new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task UploadAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();
        var dto = new UploadDocumentDto(Guid.NewGuid(), DocumentType.Soat, _today, _today.AddDays(30));

        var act = async () => await sut.UploadAsync(dto, MakeFile(), "soat.pdf");

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UploadAsync_DebeLanzarExcepcion_SiVencimientoNoEsPosteriorAEmision()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();
        var dto = new UploadDocumentDto(Guid.NewGuid(), DocumentType.Soat, _today, _today);

        var act = async () => await sut.UploadAsync(dto, MakeFile(), "soat.pdf");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UploadAsync_DebeLanzarNotFound_SiVehiculoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();
        var dto = new UploadDocumentDto(Guid.NewGuid(), DocumentType.Soat, _today, _today.AddDays(30));

        var act = async () => await sut.UploadAsync(dto, MakeFile(), "soat.pdf");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UploadAsync_DebeCalcularHashCorrecto_SubirAStorageYPersistirElDocumento()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ruta-guardada");

        var sut = CreateSut();
        var dto = new UploadDocumentDto(vehicle.Id, DocumentType.RevisionTecnica, _today, _today.AddDays(60));
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("contenido-de-prueba"))).ToLowerInvariant();

        var result = await sut.UploadAsync(dto, MakeFile(), "revision.pdf");

        result.FileHashSha256.Should().Be(expectedHash);
        result.VehicleId.Should().Be(vehicle.Id);
        result.DocumentType.Should().Be(DocumentType.RevisionTecnica);
        result.Status.Should().Be(AlertStatus.Green);

        _storageMock.Verify(s => s.UploadAsync("vehicle-documents", It.Is<string>(p => p.Contains(_tenantId.ToString())), It.IsAny<Stream>(), "application/pdf", It.IsAny<CancellationToken>()), Times.Once);
        (await _context.Documents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UploadAsync_DebeReemplazarElDocumentoExistente_DelMismoTipo()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var anterior = new VehicleDocument
        {
            TenantId = _tenantId,
            VehicleId = vehicle.Id,
            DocumentType = DocumentType.Soat,
            StoragePath = "ruta/anterior.pdf",
            IssueDate = _today.AddDays(-100),
            ExpirationDate = _today.AddDays(-1),
            FileHashSha256 = "hash-anterior"
        };
        _context.Documents.Add(anterior);
        _context.SaveChanges();

        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ruta-nueva");

        var sut = CreateSut();
        var dto = new UploadDocumentDto(vehicle.Id, DocumentType.Soat, _today, _today.AddDays(365));
        var result = await sut.UploadAsync(dto, MakeFile(), "soat-nuevo.pdf");

        (await _context.Documents.CountAsync()).Should().Be(1);
        (await _context.Documents.CountAsync(d => d.Id == anterior.Id)).Should().Be(0);
        result.IssueDate.Should().Be(_today);
        _storageMock.Verify(s => s.DeleteAsync("vehicle-documents", "ruta/anterior.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByVehicleAsync_DebeRetornarSoloLosDocumentosDelVehiculo()
    {
        var vehicle = AddVehicle();
        var otroVehiculo = Guid.NewGuid();
        _context.Documents.AddRange(
            new VehicleDocument { TenantId = _tenantId, VehicleId = vehicle.Id, DocumentType = DocumentType.Soat, ExpirationDate = _today.AddDays(10), FileHashSha256 = "a" },
            new VehicleDocument { TenantId = _tenantId, VehicleId = otroVehiculo, DocumentType = DocumentType.Soat, ExpirationDate = _today.AddDays(10), FileHashSha256 = "b" });
        _context.SaveChanges();

        var sut = CreateSut();
        var result = await sut.GetByVehicleAsync(vehicle.Id);

        result.Should().ContainSingle();
        result[0].VehicleId.Should().Be(vehicle.Id);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_DebeLanzarNotFound_SiDocumentoNoExiste()
    {
        var sut = CreateSut();

        var act = async () => await sut.GetDownloadUrlAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetDownloadUrlAsync_DebeDelegarEnStorageService_ConLaRutaGuardada()
    {
        var vehicle = AddVehicle();
        var document = new VehicleDocument
        {
            TenantId = _tenantId,
            VehicleId = vehicle.Id,
            DocumentType = DocumentType.TarjetaPropiedad,
            StoragePath = "ruta/al/archivo.pdf",
            ExpirationDate = _today.AddDays(10),
            FileHashSha256 = "hash"
        };
        _context.Documents.Add(document);
        _context.SaveChanges();

        _storageMock
            .Setup(s => s.GetSignedUrlAsync("vehicle-documents", "ruta/al/archivo.pdf", 3600, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signed-url.example/archivo.pdf");

        var sut = CreateSut();
        var url = await sut.GetDownloadUrlAsync(document.Id);

        url.Should().Be("https://signed-url.example/archivo.pdf");
    }

    [Fact]
    public async Task UpdateDatesAsync_DebeLanzarForbidden_CuandoNoEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(false);
        var sut = CreateSut();

        var act = async () => await sut.UpdateDatesAsync(Guid.NewGuid(), _today, _today.AddDays(30));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UpdateDatesAsync_DebeLanzarExcepcion_SiVencimientoNoEsPosteriorAEmision()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var act = async () => await sut.UpdateDatesAsync(Guid.NewGuid(), _today, _today);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateDatesAsync_DebeLanzarNotFound_SiDocumentoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var act = async () => await sut.UpdateDatesAsync(Guid.NewGuid(), _today, _today.AddDays(30));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateDatesAsync_DebeActualizarLasFechas_CuandoElUsuarioEsAdmin()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var document = new VehicleDocument
        {
            TenantId = _tenantId,
            VehicleId = vehicle.Id,
            DocumentType = DocumentType.Soat,
            StoragePath = "ruta/al/archivo.pdf",
            IssueDate = _today,
            ExpirationDate = _today.AddDays(10),
            FileHashSha256 = "hash"
        };
        _context.Documents.Add(document);
        _context.SaveChanges();

        var sut = CreateSut();
        var nuevaEmision = _today.AddDays(1);
        var nuevoVencimiento = _today.AddDays(90);
        var result = await sut.UpdateDatesAsync(document.Id, nuevaEmision, nuevoVencimiento);

        result.IssueDate.Should().Be(nuevaEmision);
        result.ExpirationDate.Should().Be(nuevoVencimiento);
        (await _context.Documents.FindAsync(document.Id))!.ExpirationDate.Should().Be(nuevoVencimiento);
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
    public async Task DeleteAsync_DebeLanzarNotFound_SiDocumentoNoExiste()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var sut = CreateSut();

        var act = async () => await sut.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_DebeEliminarElDocumento_YBorrarloDelStorage()
    {
        _currentUserMock.SetupGet(u => u.IsAdmin).Returns(true);
        var vehicle = AddVehicle();
        var document = new VehicleDocument
        {
            TenantId = _tenantId,
            VehicleId = vehicle.Id,
            DocumentType = DocumentType.Soat,
            StoragePath = "ruta/al/archivo.pdf",
            ExpirationDate = _today.AddDays(10),
            FileHashSha256 = "hash"
        };
        _context.Documents.Add(document);
        _context.SaveChanges();

        var sut = CreateSut();
        await sut.DeleteAsync(document.Id);

        _storageMock.Verify(s => s.DeleteAsync("vehicle-documents", "ruta/al/archivo.pdf", It.IsAny<CancellationToken>()), Times.Once);
        (await _context.Documents.CountAsync()).Should().Be(0);
    }

    public void Dispose() => _context.Dispose();
}
