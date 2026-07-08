using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetControl.Application.Services;

/// <summary>
/// Orquesta el CRUD de vehiculos. La seguridad multi-tenant NO depende de que
/// el cliente envie el TenantId: siempre se toma de ICurrentUserService, que a
/// su vez viene del JWT validado por el middleware. El filtro global de EF Core
/// (ver ApplicationDbContext) refuerza esto a nivel de query.
/// </summary>
public class VehicleService : IVehicleService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMaintenanceAlertCalculator _alertCalculator;
    private readonly IDateTimeProvider _dateTime;

    public VehicleService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IMaintenanceAlertCalculator alertCalculator,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _alertCalculator = alertCalculator;
        _dateTime = dateTime;
    }

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(CancellationToken ct = default)
    {
        var query = _db.Vehicles
            .Include(v => v.AssignedDriver)
            .Include(v => v.Documents)
            .Include(v => v.MaintenanceLogs)
            .AsQueryable();

        // Reglas de rol: el conductor solo ve su vehiculo asignado.
        if (!_currentUser.IsAdmin)
            query = query.Where(v => v.AssignedDriverId == _currentUser.UserId);

        var vehicles = await query.ToListAsync(ct);

        return vehicles.Select(MapToDto).ToList();
    }

    public async Task<VehicleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var vehicle = await FindVehicleOrThrowAsync(id, ct);
        return MapToDto(vehicle);
    }

    public async Task<VehicleDto> CreateAsync(CreateVehicleDto dto, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede registrar vehiculos.");

        var vehicle = new Vehicle
        {
            TenantId = _currentUser.TenantId,
            LicensePlate = dto.LicensePlate.ToUpperInvariant().Trim(),
            Brand = dto.Brand,
            Model = dto.Model,
            ManufactureYear = dto.ManufactureYear,
            Color = dto.Color,
            CurrentMileage = dto.CurrentMileage,
            AssignedDriverId = dto.AssignedDriverId,
            Status = VehicleStatus.Active
        };

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);

        return MapToDto(vehicle);
    }

    public async Task<VehicleDto> UpdateAsync(Guid id, UpdateVehicleDto dto, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede editar vehiculos.");

        var vehicle = await FindVehicleOrThrowAsync(id, ct);

        vehicle.Brand = dto.Brand;
        vehicle.Model = dto.Model;
        vehicle.Color = dto.Color;
        vehicle.CurrentMileage = dto.CurrentMileage;
        vehicle.AssignedDriverId = dto.AssignedDriverId;
        vehicle.Status = dto.Status;

        await _db.SaveChangesAsync(ct);
        return MapToDto(vehicle);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede eliminar vehiculos.");

        var vehicle = await FindVehicleOrThrowAsync(id, ct);
        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>El conductor reporta el kilometraje actual de su vehiculo asignado.</summary>
    public async Task<VehicleDto> ReportMileageAsync(Guid id, ReportMileageDto dto, CancellationToken ct = default)
    {
        var vehicle = await FindVehicleOrThrowAsync(id, ct);

        if (!_currentUser.IsAdmin && vehicle.AssignedDriverId != _currentUser.UserId)
            throw new ForbiddenAccessException("Solo puede reportar kilometraje de su vehiculo asignado.");

        if (dto.NewMileage < vehicle.CurrentMileage)
            throw new InvalidOperationException("El nuevo kilometraje no puede ser menor al actual.");

        vehicle.CurrentMileage = dto.NewMileage;
        await _db.SaveChangesAsync(ct);
        return MapToDto(vehicle);
    }

    private async Task<Vehicle> FindVehicleOrThrowAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles
            .Include(v => v.AssignedDriver)
            .Include(v => v.Documents)
            .Include(v => v.MaintenanceLogs)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (vehicle is null)
            throw new NotFoundException(nameof(Vehicle), id);

        if (!_currentUser.IsAdmin && vehicle.AssignedDriverId != _currentUser.UserId)
            throw new ForbiddenAccessException();

        return vehicle;
    }

    private VehicleDto MapToDto(Vehicle v)
    {
        // Estado global = el peor semaforo entre documentos y mantenimientos.
        var worst = AlertStatus.Green;
        var today = _dateTime.Today;

        foreach (var doc in v.Documents)
        {
            var s = _alertCalculator.CalculateDocumentStatus(v.Id, doc.Id, doc.DocumentType, doc.ExpirationDate, today).Status;
            if (s > worst) worst = s;
        }

        return new VehicleDto(
            v.Id, v.LicensePlate, v.Brand, v.Model, v.ManufactureYear, v.Color,
            v.CurrentMileage, v.AssignedDriverId, v.AssignedDriver?.FullName,
            v.Status, worst);
    }
}
