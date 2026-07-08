using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("vehicles");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasColumnName("id");
        b.Property(v => v.TenantId).HasColumnName("tenant_id");
        b.Property(v => v.LicensePlate).HasColumnName("license_plate").HasMaxLength(10).IsRequired();
        b.Property(v => v.Brand).HasColumnName("brand").HasMaxLength(50).IsRequired();
        b.Property(v => v.Model).HasColumnName("model").HasMaxLength(50).IsRequired();
        b.Property(v => v.ManufactureYear).HasColumnName("manufacture_year");
        b.Property(v => v.Color).HasColumnName("color").HasMaxLength(30);
        b.Property(v => v.CurrentMileage).HasColumnName("current_mileage");
        b.Property(v => v.AssignedDriverId).HasColumnName("assigned_driver_id");
        b.Property(v => v.Status).HasColumnName("status")
            .HasConversion(s => s.ToString().ToLower(), s => Enum.Parse<VehicleStatus>(s, true))
            .HasMaxLength(20);
        b.Property(v => v.CreatedAt).HasColumnName("created_at");
        b.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(v => new { v.TenantId, v.LicensePlate }).IsUnique();

        b.HasOne(v => v.Tenant).WithMany(t => t.Vehicles).HasForeignKey(v => v.TenantId);
        b.HasOne(v => v.AssignedDriver).WithMany(u => u.AssignedVehicles)
            .HasForeignKey(v => v.AssignedDriverId).OnDelete(DeleteBehavior.SetNull);
    }
}
