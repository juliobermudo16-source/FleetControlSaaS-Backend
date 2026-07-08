namespace FleetControl.Application.Common.Interfaces;

public interface IPdfReportService
{
    /// <summary>Genera el PDF consolidado de flota (QuestPDF) y devuelve los bytes.</summary>
    byte[] GenerateFleetReport(FleetReportData data);
}

public record FleetReportData(
    string CompanyName,
    DateTime GeneratedAt,
    IReadOnlyList<VehicleReportRow> Vehicles);

public record VehicleReportRow(
    string LicensePlate,
    string BrandModel,
    int CurrentMileage,
    string DriverName,
    string OverallStatus,
    decimal EstimatedMaintenanceCost);
