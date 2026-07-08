using FleetControl.Application.Common.Interfaces;
using FleetControl.Domain.Common;
using FleetControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetControl.Infrastructure.Persistence;

/// <summary>
/// DbContext principal. Aplica un Global Query Filter por TenantId en TODAS las
/// entidades que implementan ITenantEntity, usando el TenantId del usuario
/// autenticado actual (ICurrentUserService). Esto es la segunda capa de defensa
/// del aislamiento multi-tenant (la primera es RLS en PostgreSQL/Supabase).
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePhoto> VehiclePhotos => Set<VehiclePhoto>();
    public DbSet<VehicleDocument> Documents => Set<VehicleDocument>();
    public DbSet<MaintenanceType> MaintenanceTypes => Set<MaintenanceType>();
    public DbSet<MaintenanceLog> MaintenanceLogs => Set<MaintenanceLog>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global Query Filter multi-tenant: se aplica automaticamente a cada
        // consulta LINQ sobre estas entidades, sin que cada servicio tenga que
        // acordarse de filtrar por TenantId manualmente.
        if (_currentUserService is not null)
        {
            builder.Entity<Vehicle>().HasQueryFilter(v => v.TenantId == _currentUserService.TenantId);
            builder.Entity<VehiclePhoto>().HasQueryFilter(p => p.TenantId == _currentUserService.TenantId);
            builder.Entity<VehicleDocument>().HasQueryFilter(d => d.TenantId == _currentUserService.TenantId);
            builder.Entity<MaintenanceLog>().HasQueryFilter(m => m.TenantId == _currentUserService.TenantId);
            builder.Entity<IncidentReport>().HasQueryFilter(i => i.TenantId == _currentUserService.TenantId);
            builder.Entity<NotificationLog>().HasQueryFilter(n => n.TenantId == _currentUserService.TenantId);
            builder.Entity<AppUser>().HasQueryFilter(u => u.TenantId == _currentUserService.TenantId);
        }

        base.OnModelCreating(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
