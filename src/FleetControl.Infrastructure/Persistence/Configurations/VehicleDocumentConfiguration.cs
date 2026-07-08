using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetControl.Infrastructure.Persistence.Configurations;

public class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    private static readonly Dictionary<DocumentType, string> ToDb = new()
    {
        [DocumentType.Soat] = "soat",
        [DocumentType.RevisionTecnica] = "revision_tecnica",
        [DocumentType.TarjetaPropiedad] = "tarjeta_propiedad",
    };

    public void Configure(EntityTypeBuilder<VehicleDocument> b)
    {
        b.ToTable("documents");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasColumnName("id");
        b.Property(d => d.TenantId).HasColumnName("tenant_id");
        b.Property(d => d.VehicleId).HasColumnName("vehicle_id");
        b.Property(d => d.DocumentType).HasColumnName("document_type")
            .HasConversion(t => ToDb[t], s => ToDb.First(kv => kv.Value == s).Key)
            .HasMaxLength(30);
        b.Property(d => d.StoragePath).HasColumnName("storage_path").IsRequired();
        b.Property(d => d.FileHashSha256).HasColumnName("file_hash_sha256").HasMaxLength(64).IsRequired();
        b.Property(d => d.IssueDate).HasColumnName("issue_date");
        b.Property(d => d.ExpirationDate).HasColumnName("expiration_date");
        b.Property(d => d.UploadedBy).HasColumnName("uploaded_by");
        b.Property(d => d.CreatedAt).HasColumnName("created_at");
        b.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        b.HasOne(d => d.Vehicle).WithMany(v => v.Documents).HasForeignKey(d => d.VehicleId);
    }
}
