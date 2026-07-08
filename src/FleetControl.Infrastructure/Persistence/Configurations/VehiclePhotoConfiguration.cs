using FleetControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class VehiclePhotoConfiguration : IEntityTypeConfiguration<VehiclePhoto>
{
    public void Configure(EntityTypeBuilder<VehiclePhoto> b)
    {
        b.ToTable("vehicle_photos");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id");
        b.Property(p => p.TenantId).HasColumnName("tenant_id");
        b.Property(p => p.VehicleId).HasColumnName("vehicle_id");
        b.Property(p => p.StoragePath).HasColumnName("storage_path").IsRequired();
        b.Property(p => p.IsPrimary).HasColumnName("is_primary");
        b.Property(p => p.UploadedBy).HasColumnName("uploaded_by");
        b.Property(p => p.CreatedAt).HasColumnName("created_at");
        b.Ignore(p => p.UpdatedAt);

        b.HasOne(p => p.Vehicle).WithMany(v => v.Photos).HasForeignKey(p => p.VehicleId);
    }
}
