using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IMaintenanceService
{
    Task<MaintenanceLogDto> RegisterMaintenanceAsync(CreateMaintenanceLogDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceStatusDto>> GetVehicleMaintenanceStatusAsync(Guid vehicleId, CancellationToken ct = default);
}
