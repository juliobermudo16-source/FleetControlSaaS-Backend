using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class IncidentReportConfiguration : IEntityTypeConfiguration<IncidentReport>
{
    public void Configure(EntityTypeBuilder<IncidentReport> b)
    {
        b.ToTable("incident_reports");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasColumnName("id");
        b.Property(i => i.TenantId).HasColumnName("tenant_id");
        b.Property(i => i.VehicleId).HasColumnName("vehicle_id");
        b.Property(i => i.ReportedBy).HasColumnName("reported_by");
        b.Property(i => i.Description).HasColumnName("description").IsRequired();
        b.Property(i => i.Severity).HasColumnName("severity")
            .HasConversion(s => s.ToString().ToLower(), s => Enum.Parse<IncidentSeverity>(s, true)).HasMaxLength(20);
        b.Property(i => i.Status).HasColumnName("status")
            .HasConversion(s => s == IncidentStatus.InReview ? "in_review" : s.ToString().ToLower(),
                           s => s == "in_review" ? IncidentStatus.InReview : Enum.Parse<IncidentStatus>(s, true))
            .HasMaxLength(20);
        b.Property(i => i.CreatedAt).HasColumnName("created_at");
        b.Ignore(i => i.UpdatedAt);

        b.HasOne(i => i.Vehicle).WithMany().HasForeignKey(i => i.VehicleId);
    }
}
