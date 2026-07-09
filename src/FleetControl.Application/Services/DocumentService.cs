using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FleetControl.Application.Services;

/// <summary>
/// Gestiona la carga de documentos obligatorios (SOAT, Revision Tecnica,
/// Tarjeta de Propiedad). Calcula el hash SHA-256 del archivo ANTES de subirlo
/// para dejar constancia de auditoria e integridad (si el archivo cambia
/// despues, el hash guardado en BD ya no coincidira con uno recalculado).
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISupabaseStorageService _storage;
    private readonly IMaintenanceAlertCalculator _calculator;
    private readonly IDateTimeProvider _dateTime;

    private const string Bucket = "vehicle-documents";

    public DocumentService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISupabaseStorageService storage,
        IMaintenanceAlertCalculator calculator,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _calculator = calculator;
        _dateTime = dateTime;
    }

    public async Task<DocumentDto> UploadAsync(UploadDocumentDto dto, Stream fileContent, string fileName, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede cargar documentos.");

        if (dto.ExpirationDate <= dto.IssueDate)
            throw new InvalidOperationException("La fecha de vencimiento debe ser posterior a la fecha de emision.");

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == dto.VehicleId, ct)
            ?? throw new NotFoundException(nameof(Vehicle), dto.VehicleId);

        // 1. Calcular hash SHA-256 del archivo original (antes de subir).
        var hashBytes = await SHA256.HashDataAsync(fileContent, ct);
        var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        fileContent.Position = 0;

        // 2. Un vehiculo solo puede tener UN documento vigente por tipo (SOAT,
        // Revision Tecnica, Tarjeta de Propiedad), para no confundir la UI con
        // filas duplicadas. El/los anteriores no se borran (se conservan como
        // historial, ej. SOATs de años previos), solo dejan de ser "vigentes".
        var currentOnes = await _db.Documents
            .Where(d => d.VehicleId == dto.VehicleId && d.DocumentType == dto.DocumentType && d.IsCurrent)
            .ToListAsync(ct);
        foreach (var old in currentOnes)
            old.IsCurrent = false;

        // 3. Subir a Supabase Storage (bucket privado).
        var storagePath = $"{_currentUser.TenantId}/{dto.VehicleId}/{dto.DocumentType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";
        await _storage.UploadAsync(Bucket, storagePath, fileContent, "application/pdf", ct);

        // 4. Persistir metadatos + hash.
        var document = new VehicleDocument
        {
            TenantId = _currentUser.TenantId,
            VehicleId = dto.VehicleId,
            DocumentType = dto.DocumentType,
            StoragePath = storagePath,
            FileHashSha256 = fileHash,
            IssueDate = dto.IssueDate,
            ExpirationDate = dto.ExpirationDate,
            UploadedBy = _currentUser.UserId
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        return MapToDto(document);
    }

    public async Task<IReadOnlyList<DocumentDto>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var docs = await _db.Documents.Where(d => d.VehicleId == vehicleId && d.IsCurrent).ToListAsync(ct);
        return docs.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<DocumentDto>> GetHistoryByTypeAsync(Guid vehicleId, DocumentType documentType, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.VehicleId == vehicleId && d.DocumentType == documentType)
            .OrderByDescending(d => d.IssueDate)
            .ToListAsync(ct);
        return docs.Select(MapToDto).ToList();
    }

    public async Task<string> GetDownloadUrlAsync(Guid documentId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException(nameof(VehicleDocument), documentId);

        return await _storage.GetSignedUrlAsync(Bucket, doc.StoragePath, 3600, ct);
    }

    public async Task<DocumentDto> UpdateDatesAsync(Guid documentId, DateOnly issueDate, DateOnly expirationDate, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede editar documentos.");

        if (expirationDate <= issueDate)
            throw new InvalidOperationException("La fecha de vencimiento debe ser posterior a la fecha de emision.");

        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException(nameof(VehicleDocument), documentId);

        doc.IssueDate = issueDate;
        doc.ExpirationDate = expirationDate;
        await _db.SaveChangesAsync(ct);

        return MapToDto(doc);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede eliminar documentos.");

        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException(nameof(VehicleDocument), documentId);

        await _storage.DeleteAsync(Bucket, doc.StoragePath, ct);
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync(ct);
    }

    private DocumentDto MapToDto(VehicleDocument d)
    {
        var status = _calculator.CalculateDocumentStatus(d.VehicleId, d.Id, d.DocumentType, d.ExpirationDate, _dateTime.Today);
        return new DocumentDto(d.Id, d.VehicleId, d.DocumentType, d.IssueDate, d.ExpirationDate, d.FileHashSha256, status.Status, status.DaysUntilExpiration, d.IsCurrent);
    }
}
