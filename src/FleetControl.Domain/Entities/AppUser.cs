using FleetControl.Domain.Common;
using FleetControl.Domain.Enums;

namespace FleetControl.Domain.Entities;

/// <summary>
/// Perfil de negocio del usuario. El Id es el MISMO Guid que auth.users.id
/// en Supabase Auth (no se generan credenciales aqui, solo se refleja el perfil).
/// </summary>
public class AppUser : Common.ITenantEntity
{
    public Guid Id { get; set; } // = auth.users.id de Supabase
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Driver;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public ICollection<Vehicle> AssignedVehicles { get; set; } = new List<Vehicle>();
}
