using FleetControl.Domain.Enums;

namespace FleetControl.Application.DTOs;

/// <summary>Resultado del calculo de semaforo para UN mantenimiento de UN vehiculo.</summary>
public record MaintenanceStatusDto(
    Guid VehicleId,
    Guid MaintenanceTypeId,
    string MaintenanceTypeName,
    int CurrentMileage,
    int LastServiceMileage,
    int IntervalKm,
    double WearPercentage,      // ((KmActual - KmUltimoServicio) / IntervaloKm) * 100
    AlertStatus Status,
    int KmRemaining             // km que faltan para llegar al 100% (puede ser negativo si ya paso)
)
{
    /// <summary>Fecha del ultimo servicio registrado (null si nunca se ha hecho).</summary>
    public DateOnly? LastServiceDate { get; init; }
}

/// <summary>Resultado del calculo de semaforo para UN documento (SOAT, Revision Tecnica, etc).</summary>
public record DocumentStatusDto(
    Guid VehicleId,
    Guid DocumentId,
    DocumentType DocumentType,
    DateOnly ExpirationDate,
    int DaysUntilExpiration,
    AlertStatus Status
);

/// <summary>Resumen combinado de un vehiculo: el peor estado entre todos sus items.</summary>
public record VehicleAlertSummaryDto(
    Guid VehicleId,
    string LicensePlate,
    AlertStatus OverallStatus,
    IReadOnlyList<MaintenanceStatusDto> MaintenanceItems,
    IReadOnlyList<DocumentStatusDto> DocumentItems
);
