using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasColumnName("id");
        b.Property(u => u.TenantId).HasColumnName("tenant_id");
        b.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
        b.Property(u => u.Email).HasColumnName("email").HasMaxLength(150).IsRequired();
        b.Property(u => u.Role).HasColumnName("role")
            .HasConversion(r => r == UserRole.Admin ? "admin" : "driver",
                           s => s == "admin" ? UserRole.Admin : UserRole.Driver)
            .HasMaxLength(20);
        b.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(20);
        b.Property(u => u.AvatarStoragePath).HasColumnName("avatar_storage_path");
        b.Property(u => u.IsActive).HasColumnName("is_active");
        b.Property(u => u.PendingDeletionAt).HasColumnName("pending_deletion_at");
        b.Property(u => u.CreatedAt).HasColumnName("created_at");
        b.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        b.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
    }
}
