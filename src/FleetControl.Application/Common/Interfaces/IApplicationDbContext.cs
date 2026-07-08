using FleetControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetControl.Application.Common.Interfaces;

/// <summary>
/// Abstraccion del DbContext para que Application no dependa de EF Core/Infrastructure
/// directamente en su firma (solo se referencia el paquete EFCore.Abstractions via DbSet).
/// Infrastructure.Persistence.ApplicationDbContext la implementa.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<AppUser> Users { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehiclePhoto> VehiclePhotos { get; }
    DbSet<VehicleDocument> Documents { get; }
    DbSet<MaintenanceType> MaintenanceTypes { get; }
    DbSet<MaintenanceLog> MaintenanceLogs { get; }
    DbSet<IncidentReport> IncidentReports { get; }
    DbSet<NotificationLog> NotificationLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
