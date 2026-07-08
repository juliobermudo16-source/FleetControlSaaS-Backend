using FleetControl.Application.DTOs;
using FleetControl.Domain.Enums;

namespace FleetControl.Application.Services;

/// <summary>
/// Logica de negocio CENTRAL del sistema: calcula el estado de semaforo
/// (Verde/Amarillo/Rojo) tanto para mantenimientos preventivos como para
/// documentos, segun las reglas fijadas por el negocio:
///
///   Mantenimientos:
///     % Desgaste = ((KmActual - KmUltimoMantenimiento) / IntervaloKm) * 100
///     - Verde  : % Desgaste &lt;= 80
///     - Amarillo: 80 &lt; % Desgaste &lt;= 100
///     - Rojo   : % Desgaste &gt; 100
///
///   Documentos:
///     - Verde  : faltan mas de 30 dias para vencer
///     - Amarillo: faltan 30 dias o menos (pero no vencido)
///     - Rojo   : ya vencido (dias restantes &lt;= 0)
///
/// No tiene dependencias externas (ni EF Core, ni HTTP, ni fecha del sistema
/// directa) por lo que es 100% testeable con xUnit sin mocks para esta clase
/// (la fecha "hoy" se recibe como parametro, inyectada por IDateTimeProvider
/// en la capa que la invoca).
/// </summary>
public class MaintenanceAlertCalculator : IMaintenanceAlertCalculator
{
    private const double YellowThresholdPercentage = 80.0;
    private const double RedThresholdPercentage = 100.0;
    private const int DocumentYellowThresholdDays = 30;

    public MaintenanceStatusDto CalculateMaintenanceStatus(
        Guid vehicleId,
        Guid maintenanceTypeId,
        string maintenanceTypeName,
        int currentMileage,
        int lastServiceMileage,
        int intervalKm)
    {
        if (intervalKm <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalKm), "El intervalo de kilometraje debe ser mayor a 0.");

        var kmSinceService = currentMileage - lastServiceMileage;
        // Si por alguna razon el kilometraje registrado es menor al del ultimo
        // servicio (dato corregido, odometro reseteado, etc.) no se permite
        // desgaste negativo: se trata como 0% recorrido.
        if (kmSinceService < 0) kmSinceService = 0;

        var wearPercentage = (kmSinceService / (double)intervalKm) * 100.0;

        var status = wearPercentage switch
        {
            > RedThresholdPercentage => AlertStatus.Red,
            > YellowThresholdPercentage => AlertStatus.Yellow,
            _ => AlertStatus.Green
        };

        var kmRemaining = intervalKm - kmSinceService;

        return new MaintenanceStatusDto(
            VehicleId: vehicleId,
            MaintenanceTypeId: maintenanceTypeId,
            MaintenanceTypeName: maintenanceTypeName,
            CurrentMileage: currentMileage,
            LastServiceMileage: lastServiceMileage,
            IntervalKm: intervalKm,
            WearPercentage: Math.Round(wearPercentage, 2),
            Status: status,
            KmRemaining: kmRemaining);
    }

    public DocumentStatusDto CalculateDocumentStatus(
        Guid vehicleId,
        Guid documentId,
        DocumentType documentType,
        DateOnly expirationDate,
        DateOnly today)
    {
        var daysUntilExpiration = expirationDate.DayNumber - today.DayNumber;

        var status = daysUntilExpiration switch
        {
            <= 0 => AlertStatus.Red,                                   // vencido
            _ when daysUntilExpiration <= DocumentYellowThresholdDays => AlertStatus.Yellow,
            _ => AlertStatus.Green
        };

        return new DocumentStatusDto(
            VehicleId: vehicleId,
            DocumentId: documentId,
            DocumentType: documentType,
            ExpirationDate: expirationDate,
            DaysUntilExpiration: daysUntilExpiration,
            Status: status);
    }
}
