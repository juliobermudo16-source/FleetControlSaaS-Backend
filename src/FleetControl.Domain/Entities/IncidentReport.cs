using FleetControl.Domain.Common;
using FleetControl.Domain.Enums;

namespace FleetControl.Domain.Entities;

public class IncidentReport : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid ReportedBy { get; set; }
    public string Description { get; set; } = string.Empty;
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Low;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;

    public Vehicle? Vehicle { get; set; }
}
