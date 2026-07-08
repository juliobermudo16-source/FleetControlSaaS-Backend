namespace FleetControl.Domain.Common;

/// <summary>
/// Marca una entidad como perteneciente a un Tenant (aislamiento multi-tenant).
/// EF Core aplica un Global Query Filter sobre TenantId para todas las entidades
/// que implementen esta interfaz (ver ApplicationDbContext).
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
