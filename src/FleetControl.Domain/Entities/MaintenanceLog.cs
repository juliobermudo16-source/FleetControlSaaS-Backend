using FleetControl.Domain.Common;

namespace FleetControl.Domain.Entities;

public class MaintenanceLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public int MileageAtService { get; set; }
    public DateOnly ServiceDate { get; set; }
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
    public Guid? PerformedBy { get; set; }

    public Vehicle? Vehicle { get; set; }
    public MaintenanceType? MaintenanceType { get; set; }
}
