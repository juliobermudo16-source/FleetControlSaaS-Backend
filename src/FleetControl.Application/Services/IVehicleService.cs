using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(CancellationToken ct = default);
    Task<VehicleDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<VehicleDto> CreateAsync(CreateVehicleDto dto, CancellationToken ct = default);
    Task<VehicleDto> UpdateAsync(Guid id, UpdateVehicleDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<VehicleDto> ReportMileageAsync(Guid id, ReportMileageDto dto, CancellationToken ct = default);
}
