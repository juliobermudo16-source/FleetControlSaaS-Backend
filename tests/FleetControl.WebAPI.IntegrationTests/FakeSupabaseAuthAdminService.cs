using FleetControl.Application.Common.Interfaces;

namespace FleetControl.WebAPI.IntegrationTests;

/// <summary>
/// Reemplazo en memoria de ISupabaseAuthAdminService para pruebas de
/// integracion: el servicio real llama a la API de administracion de
/// Supabase Auth (necesita credenciales reales), asi que aqui simplemente
/// se genera un Guid nuevo, como si Supabase hubiera creado el usuario.
/// </summary>
public class FakeSupabaseAuthAdminService : ISupabaseAuthAdminService
{
    public Task<Guid> InviteUserByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
        => Task.CompletedTask;
}
