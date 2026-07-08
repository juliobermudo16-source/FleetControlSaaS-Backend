using FleetControl.Application.Common.Interfaces;

namespace FleetControl.Infrastructure.Identity;

/// <summary>
/// Implementacion Scoped (una instancia por request HTTP) de ICurrentUserService.
/// Es poblada por SupabaseJwtMiddleware (en WebAPI) tras validar el JWT y
/// resolver el TenantId/Role del usuario contra la tabla users. El resto de la
/// aplicacion (servicios, DbContext) la consume solo de lectura.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Role { get; set; } = "driver";
    public string Email { get; set; } = string.Empty;
}
