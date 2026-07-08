using FleetControl.Domain.Common;

namespace FleetControl.Domain.Entities;

public class Tenant : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public bool IsActive { get; set; } = true;
    public string SubscriptionPlan { get; set; } = "free";

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
