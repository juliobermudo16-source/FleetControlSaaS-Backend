using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetControl.Application.Services;

/// <summary>
/// Construye el resumen ejecutivo para el Dashboard: distribucion verde/amarillo/rojo,
/// costo estimado de mantenimientos proximos y lista de vehiculos urgentes.
/// Solo accesible para Administradores (se valida en el Controller via [Authorize(Roles="admin")]).
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMaintenanceAlertCalculator _calculator;
    private readonly IDateTimeProvider _dateTime;

    public DashboardService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IMaintenanceAlertCalculator calculator,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _calculator = calculator;
        _dateTime = dateTime;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var vehicles = await _db.Vehicles
            .Include(v => v.Documents)
            .Include(v => v.MaintenanceLogs)
            .ToListAsync(ct);

        var maintenanceTypes = await _db.MaintenanceTypes
            .Where(m => m.TenantId == null || m.TenantId == _currentUser.TenantId)
            .ToListAsync(ct);

        var today = _dateTime.Today;
        int green = 0, yellow = 0, red = 0;
        decimal upcomingCost = 0;
        var urgent = new List<VehicleAlertSummaryDto>();

        foreach (var vehicle in vehicles)
        {
            var maintenanceItems = new List<MaintenanceStatusDto>();
            foreach (var type in maintenanceTypes)
            {
                var lastMileage = vehicle.MaintenanceLogs
                    .Where(l => l.MaintenanceTypeId == type.Id)
                    .OrderByDescending(l => l.MileageAtService)
                    .Select(l => (int?)l.MileageAtService)
                    .FirstOrDefault() ?? 0;

                var status = _calculator.CalculateMaintenanceStatus(
                    vehicle.Id, type.Id, type.Name, vehicle.CurrentMileage, lastMileage, type.IntervalKm);

                maintenanceItems.Add(status);

                if (status.Status is AlertStatus.Yellow or AlertStatus.Red)
                    upcomingCost += type.EstimatedCost;
            }

            var documentItems = vehicle.Documents
                .Select(d => _calculator.CalculateDocumentStatus(vehicle.Id, d.Id, d.DocumentType, d.ExpirationDate, today))
                .ToList();

            var worst = AlertStatus.Green;
            foreach (var m in maintenanceItems) if (m.Status > worst) worst = m.Status;
            foreach (var d in documentItems) if (d.Status > worst) worst = d.Status;

            switch (worst)
            {
                case AlertStatus.Green: green++; break;
                case AlertStatus.Yellow: yellow++; break;
                case AlertStatus.Red: red++; break;
            }

            if (worst is AlertStatus.Yellow or AlertStatus.Red)
            {
                urgent.Add(new VehicleAlertSummaryDto(vehicle.Id, vehicle.LicensePlate, worst, maintenanceItems, documentItems));
            }
        }

        return new DashboardSummaryDto(
            TotalVehicles: vehicles.Count,
            GreenCount: green,
            YellowCount: yellow,
            RedCount: red,
            EstimatedUpcomingCost: upcomingCost,
            UrgentVehicles: urgent.OrderByDescending(u => u.OverallStatus).ToList());
    }
}
