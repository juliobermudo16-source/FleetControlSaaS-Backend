using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
using FleetControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetControl.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMaintenanceAlertCalculator _calculator;

    public MaintenanceService(IApplicationDbContext db, ICurrentUserService currentUser, IMaintenanceAlertCalculator calculator)
    {
        _db = db;
        _currentUser = currentUser;
        _calculator = calculator;
    }

    public async Task<MaintenanceLogDto> RegisterMaintenanceAsync(CreateMaintenanceLogDto dto, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede registrar mantenimientos.");

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == dto.VehicleId, ct)
            ?? throw new NotFoundException(nameof(Vehicle), dto.VehicleId);

        var maintenanceType = await _db.MaintenanceTypes.FirstOrDefaultAsync(m => m.Id == dto.MaintenanceTypeId, ct)
            ?? throw new NotFoundException(nameof(MaintenanceType), dto.MaintenanceTypeId);

        var log = new MaintenanceLog
        {
            TenantId = _currentUser.TenantId,
            VehicleId = dto.VehicleId,
            MaintenanceTypeId = dto.MaintenanceTypeId,
            MileageAtService = dto.MileageAtService,
            ServiceDate = dto.ServiceDate,
            Cost = dto.Cost,
            Notes = dto.Notes,
            PerformedBy = _currentUser.UserId
        };

        _db.MaintenanceLogs.Add(log);

        // Si el mantenimiento se registra con un kilometraje mayor al actual del
        // vehiculo, se actualiza el odometro (fuente de verdad mas reciente).
        if (dto.MileageAtService > vehicle.CurrentMileage)
            vehicle.CurrentMileage = dto.MileageAtService;

        await _db.SaveChangesAsync(ct);

        return new MaintenanceLogDto(log.Id, log.VehicleId, maintenanceType.Name, log.MileageAtService, log.ServiceDate, log.Cost, log.Notes);
    }

    public async Task<IReadOnlyList<MaintenanceStatusDto>> GetVehicleMaintenanceStatusAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (!_currentUser.IsAdmin && vehicle.AssignedDriverId != _currentUser.UserId)
            throw new ForbiddenAccessException();

        var maintenanceTypes = await _db.MaintenanceTypes
            .Where(m => m.TenantId == null || m.TenantId == _currentUser.TenantId)
            .ToListAsync(ct);

        var results = new List<MaintenanceStatusDto>();

        foreach (var type in maintenanceTypes)
        {
            var lastLog = await _db.MaintenanceLogs
                .Where(l => l.VehicleId == vehicleId && l.MaintenanceTypeId == type.Id)
                .OrderByDescending(l => l.MileageAtService)
                .FirstOrDefaultAsync(ct);

            var lastServiceMileage = lastLog?.MileageAtService ?? 0;

            var status = _calculator.CalculateMaintenanceStatus(
                vehicleId, type.Id, type.Name, vehicle.CurrentMileage, lastServiceMileage, type.IntervalKm);

            results.Add(status with { LastServiceDate = lastLog?.ServiceDate });
        }

        return results;
    }

    public async Task<IReadOnlyList<MaintenanceLogDto>> GetVehicleMaintenanceHistoryAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        if (!_currentUser.IsAdmin && vehicle.AssignedDriverId != _currentUser.UserId)
            throw new ForbiddenAccessException();

        var logs = await _db.MaintenanceLogs
            .Where(l => l.VehicleId == vehicleId)
            .Include(l => l.MaintenanceType)
            .OrderByDescending(l => l.ServiceDate)
            .ThenByDescending(l => l.MileageAtService)
            .ToListAsync(ct);

        return logs
            .Select(l => new MaintenanceLogDto(l.Id, l.VehicleId, l.MaintenanceType?.Name ?? string.Empty, l.MileageAtService, l.ServiceDate, l.Cost, l.Notes))
            .ToList();
    }
}
