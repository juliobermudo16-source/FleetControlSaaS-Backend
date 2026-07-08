namespace FleetControl.Application.DTOs;

public record DashboardSummaryDto(
    int TotalVehicles,
    int GreenCount,
    int YellowCount,
    int RedCount,
    decimal EstimatedUpcomingCost,
    IReadOnlyList<VehicleAlertSummaryDto> UrgentVehicles); // rojo o amarillo
