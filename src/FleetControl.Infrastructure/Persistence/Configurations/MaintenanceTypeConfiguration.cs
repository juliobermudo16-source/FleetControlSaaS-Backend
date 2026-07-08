using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class MaintenanceTypeConfiguration : IEntityTypeConfiguration<MaintenanceType>
{
    private static readonly Dictionary<MaintenanceTypeCode, string> ToDb = new()
    {
        [MaintenanceTypeCode.OilChange] = "oil_change",
        [MaintenanceTypeCode.BrakePads] = "brake_pads",
        [MaintenanceTypeCode.General] = "general",
    };

    public void Configure(EntityTypeBuilder<MaintenanceType> b)
    {
        b.ToTable("maintenance_types");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id");
        b.Property(m => m.TenantId).HasColumnName("tenant_id");
        b.Property(m => m.Code).HasColumnName("code")
            .HasConversion(c => ToDb[c], s => ToDb.First(kv => kv.Value == s).Key)
            .HasMaxLength(30);
        b.Property(m => m.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(m => m.IntervalKm).HasColumnName("interval_km");
        b.Property(m => m.EstimatedCost).HasColumnName("estimated_cost").HasColumnType("numeric(10,2)");
        b.Property(m => m.CreatedAt).HasColumnName("created_at");
        b.Ignore(m => m.UpdatedAt);
    }
}
