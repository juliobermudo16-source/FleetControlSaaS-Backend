using FleetControl.Domain.Common;
using FleetControl.Domain.Enums;

namespace FleetControl.Domain.Entities;

public class MaintenanceType : BaseEntity
{
    public Guid? TenantId { get; set; } // null = catalogo global
    public MaintenanceTypeCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int IntervalKm { get; set; } // ej. 5000
    public decimal EstimatedCost { get; set; }
}
