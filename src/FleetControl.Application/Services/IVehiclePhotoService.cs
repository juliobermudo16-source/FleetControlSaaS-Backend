using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IVehiclePhotoService
{
    Task<VehiclePhotoDto> UploadAsync(UploadPhotoDto dto, Stream fileContent, string fileName, CancellationToken ct = default);
    Task<IReadOnlyList<VehiclePhotoDto>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task DeleteAsync(Guid photoId, CancellationToken ct = default);
}
