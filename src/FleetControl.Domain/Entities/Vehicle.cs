using FleetControl.Domain.Common;
using FleetControl.Domain.Enums;

namespace FleetControl.Domain.Entities;

public class Vehicle : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public short ManufactureYear { get; set; }
    public string? Color { get; set; }
    public int CurrentMileage { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Active;

    public Tenant? Tenant { get; set; }
    public AppUser? AssignedDriver { get; set; }
    public ICollection<VehiclePhoto> Photos { get; set; } = new List<VehiclePhoto>();
    public ICollection<VehicleDocument> Documents { get; set; } = new List<VehicleDocument>();
    public ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();
}
