using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> b)
    {
        b.ToTable("notification_logs");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasColumnName("id");
        b.Property(n => n.TenantId).HasColumnName("tenant_id");
        b.Property(n => n.VehicleId).HasColumnName("vehicle_id");
        b.Property(n => n.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(150).IsRequired();
        b.Property(n => n.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        b.Property(n => n.AlertStatus).HasColumnName("alert_status")
            .HasConversion(s => s == AlertStatus.Red ? "red" : "yellow",
                           s => s == "red" ? AlertStatus.Red : AlertStatus.Yellow)
            .HasMaxLength(10);
        b.Property(n => n.SentAt).HasColumnName("sent_at");
        b.Ignore(n => n.CreatedAt);
        b.Ignore(n => n.UpdatedAt);
    }
}
