using FleetControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class MaintenanceLogConfiguration : IEntityTypeConfiguration<MaintenanceLog>
{
    public void Configure(EntityTypeBuilder<MaintenanceLog> b)
    {
        b.ToTable("maintenance_logs");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id");
        b.Property(m => m.TenantId).HasColumnName("tenant_id");
        b.Property(m => m.VehicleId).HasColumnName("vehicle_id");
        b.Property(m => m.MaintenanceTypeId).HasColumnName("maintenance_type_id");
        b.Property(m => m.MileageAtService).HasColumnName("mileage_at_service");
        b.Property(m => m.ServiceDate).HasColumnName("service_date");
        b.Property(m => m.Cost).HasColumnName("cost").HasColumnType("numeric(10,2)");
        b.Property(m => m.Notes).HasColumnName("notes");
        b.Property(m => m.PerformedBy).HasColumnName("performed_by");
        b.Property(m => m.CreatedAt).HasColumnName("created_at");
        b.Ignore(m => m.UpdatedAt);

        b.HasOne(m => m.Vehicle).WithMany(v => v.MaintenanceLogs).HasForeignKey(m => m.VehicleId);
        b.HasOne(m => m.MaintenanceType).WithMany().HasForeignKey(m => m.MaintenanceTypeId);
    }
}
