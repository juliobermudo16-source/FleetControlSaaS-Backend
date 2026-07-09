namespace FleetControl.Application.Common.Interfaces;

/// <summary>
/// Cliente hacia la API de administracion de Supabase Auth (usa el
/// service_role key). Se usa para invitar usuarios nuevos (crea el registro
/// en auth.users y Supabase envia un correo con un enlace magico) y para
/// borrarlos permanentemente cuando se cumple el plazo de eliminacion.
/// </summary>
public interface ISupabaseAuthAdminService
{
    /// <summary>Crea el usuario en Supabase Auth y envia el correo de invitacion. Devuelve su Id (Guid).</summary>
    Task<Guid> InviteUserByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Borra permanentemente al usuario de Supabase Auth (ya no podra iniciar sesion nunca mas).</summary>
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
