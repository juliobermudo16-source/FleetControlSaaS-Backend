using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
using FleetControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetControl.Application.Services;

/// <summary>
/// Gestiona las fotos de vehiculos (bucket publico 'vehicle-photos'). A
/// diferencia de los documentos, las fotos no necesitan hash de integridad
/// ni URL firmada: el bucket es publico, asi que la URL es permanente.
/// </summary>
public class VehiclePhotoService : IVehiclePhotoService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISupabaseStorageService _storage;

    private const string Bucket = "vehicle-photos";

    public VehiclePhotoService(IApplicationDbContext db, ICurrentUserService currentUser, ISupabaseStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<VehiclePhotoDto> UploadAsync(UploadPhotoDto dto, Stream fileContent, string fileName, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede subir fotos.");

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == dto.VehicleId, ct)
            ?? throw new NotFoundException(nameof(Vehicle), dto.VehicleId);

        var storagePath = $"{_currentUser.TenantId}/{dto.VehicleId}/{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";
        await _storage.UploadAsync(Bucket, storagePath, fileContent, "image/jpeg", ct);

        if (dto.IsPrimary)
        {
            var existingPrimary = await _db.VehiclePhotos.Where(p => p.VehicleId == dto.VehicleId && p.IsPrimary).ToListAsync(ct);
            foreach (var p in existingPrimary) p.IsPrimary = false;
        }

        var photo = new VehiclePhoto
        {
            TenantId = _currentUser.TenantId,
            VehicleId = dto.VehicleId,
            StoragePath = storagePath,
            IsPrimary = dto.IsPrimary,
            UploadedBy = _currentUser.UserId
        };

        _db.VehiclePhotos.Add(photo);
        await _db.SaveChangesAsync(ct);

        return MapToDto(photo);
    }

    public async Task<IReadOnlyList<VehiclePhotoDto>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var photos = await _db.VehiclePhotos
            .Where(p => p.VehicleId == vehicleId)
            .OrderByDescending(p => p.IsPrimary)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return photos.Select(MapToDto).ToList();
    }

    public async Task DeleteAsync(Guid photoId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede eliminar fotos.");

        var photo = await _db.VehiclePhotos.FirstOrDefaultAsync(p => p.Id == photoId, ct)
            ?? throw new NotFoundException(nameof(VehiclePhoto), photoId);

        await _storage.DeleteAsync(Bucket, photo.StoragePath, ct);
        _db.VehiclePhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);
    }

    private VehiclePhotoDto MapToDto(VehiclePhoto p) =>
        new(p.Id, p.VehicleId, _storage.GetPublicUrl(Bucket, p.StoragePath), p.IsPrimary);
}
