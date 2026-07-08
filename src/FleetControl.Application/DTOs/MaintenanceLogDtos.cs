namespace FleetControl.Application.DTOs;

public record CreateMaintenanceLogDto(
    Guid VehicleId,
    Guid MaintenanceTypeId,
    int MileageAtService,
    DateOnly ServiceDate,
    decimal Cost,
    string? Notes);

public record MaintenanceLogDto(
    Guid Id,
    Guid VehicleId,
    string MaintenanceTypeName,
    int MileageAtService,
    DateOnly ServiceDate,
    decimal Cost,
    string? Notes);
