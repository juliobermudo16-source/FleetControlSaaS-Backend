using FleetControl.Domain.Common;
using FleetControl.Domain.Enums;

namespace FleetControl.Domain.Entities;

public class NotificationLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? VehicleId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public AlertStatus AlertStatus { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
