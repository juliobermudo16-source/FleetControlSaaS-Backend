using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IUserService
{
    Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Lista los usuarios del tenant actual (solo Admin).</summary>
    Task<IReadOnlyList<UserDto>> GetTenantUsersAsync(CancellationToken ct = default);

    /// <summary>Invita a un usuario nuevo por correo (solo Admin): crea el registro en Supabase Auth y el perfil de negocio.</summary>
    Task<UserDto> InviteUserAsync(InviteUserDto dto, CancellationToken ct = default);

    /// <summary>Desactiva un usuario del tenant (solo Admin): pierde acceso y se desasigna de sus vehiculos. No borra su historial.</summary>
    Task DeactivateUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Reactiva un usuario previamente desactivado (solo Admin).</summary>
    Task ReactivateUserAsync(Guid userId, CancellationToken ct = default);
}
