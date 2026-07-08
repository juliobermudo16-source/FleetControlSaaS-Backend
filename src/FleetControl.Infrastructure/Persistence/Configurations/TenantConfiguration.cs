using FleetControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id");
        b.Property(t => t.CompanyName).HasColumnName("company_name").HasMaxLength(150).IsRequired();
        b.Property(t => t.Ruc).HasColumnName("ruc").HasMaxLength(20);
        b.Property(t => t.IsActive).HasColumnName("is_active");
        b.Property(t => t.SubscriptionPlan).HasColumnName("subscription_plan").HasMaxLength(30);
        b.Property(t => t.CreatedAt).HasColumnName("created_at");
        b.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
