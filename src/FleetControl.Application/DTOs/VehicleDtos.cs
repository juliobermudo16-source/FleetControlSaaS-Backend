using FleetControl.Domain.Enums;

namespace FleetControl.Application.DTOs;

public record VehicleDto(
    Guid Id,
    string LicensePlate,
    string Brand,
    string Model,
    short ManufactureYear,
    string? Color,
    int CurrentMileage,
    Guid? AssignedDriverId,
    string? AssignedDriverName,
    VehicleStatus Status,
    AlertStatus OverallAlertStatus);

public record CreateVehicleDto(
    string LicensePlate,
    string Brand,
    string Model,
    short ManufactureYear,
    string? Color,
    int CurrentMileage,
    Guid? AssignedDriverId);

public record UpdateVehicleDto(
    string Brand,
    string Model,
    string? Color,
    int CurrentMileage,
    Guid? AssignedDriverId,
    VehicleStatus Status);

public record ReportMileageDto(int NewMileage);
