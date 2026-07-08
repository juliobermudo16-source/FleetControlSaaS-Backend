using FleetControl.Domain.Common;

namespace FleetControl.Domain.Entities;

public class VehiclePhoto : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public string StoragePath { get; set; } = string.Empty; // bucket 'vehicle-photos'
    public bool IsPrimary { get; set; }
    public Guid? UploadedBy { get; set; }

    public Vehicle? Vehicle { get; set; }
}
