using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IUserService
{
    Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Lista los usuarios del tenant actual (solo Admin).</summary>
    Task<IReadOnlyList<UserDto>> GetTenantUsersAsync(CancellationToken ct = default);

    /// <summary>Invita a un usuario nuevo por correo (solo Admin): crea el registro en Supabase Auth y el perfil de negocio.</summary>
    Task<UserDto> InviteUserAsync(InviteUserDto dto, CancellationToken ct = default);

    /// <summary>
    /// Programa el borrado permanente de un usuario del tenant (solo Admin):
    /// pierde acceso inmediatamente y se desasigna de sus vehiculos, pero el
    /// registro (y su cuenta de Supabase Auth) solo se elimina de verdad
    /// cuando se cumple el plazo (ver UserDeletionBackgroundService).
    /// </summary>
    Task DeactivateUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Cancela un borrado programado y restaura el acceso (solo Admin), mientras aun no se haya ejecutado.</summary>
    Task ReactivateUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Actualiza el nombre y telefono del usuario autenticado (cualquier rol puede editar los suyos).</summary>
    Task<UserDto> UpdateMyProfileAsync(UpdateProfileDto dto, CancellationToken ct = default);

    /// <summary>Sube/reemplaza la foto de perfil del usuario autenticado (bucket publico 'user-avatars').</summary>
    Task<UserDto> UploadMyAvatarAsync(Stream fileContent, string fileName, CancellationToken ct = default);
}
