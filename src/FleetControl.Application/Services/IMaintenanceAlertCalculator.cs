using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IMaintenanceAlertCalculator
{
    MaintenanceStatusDto CalculateMaintenanceStatus(
        Guid vehicleId,
        Guid maintenanceTypeId,
        string maintenanceTypeName,
        int currentMileage,
        int lastServiceMileage,
        int intervalKm);

    DocumentStatusDto CalculateDocumentStatus(
        Guid vehicleId,
        Guid documentId,
        Domain.Enums.DocumentType documentType,
        DateOnly expirationDate,
        DateOnly today);
}
