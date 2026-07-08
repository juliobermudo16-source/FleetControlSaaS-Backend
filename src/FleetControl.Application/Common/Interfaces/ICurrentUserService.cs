namespace FleetControl.Application.Common.Interfaces;

/// <summary>
/// Expone la identidad del usuario autenticado, extraida del JWT de Supabase
/// por el SupabaseJwtMiddleware (Infrastructure/WebAPI). Es la pieza clave
/// para el aislamiento multi-tenant: TODO servicio de Application filtra por
/// TenantId usando esta interfaz, nunca confiando en un valor enviado por el cliente.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string Role { get; }         // "admin" | "driver"
    string Email { get; }
    bool IsAdmin => Role == "admin";
}
