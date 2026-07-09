using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FleetControl.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FleetControl.Infrastructure.Services;

/// <summary>
/// Llama a la API de administracion de Supabase Auth (POST /auth/v1/invite)
/// usando el service_role key. Supabase crea el usuario en auth.users y le
/// envia un correo con un enlace magico; el link redirige a la "Site URL" /
/// "Redirect URLs" configuradas en el dashboard de Supabase (Authentication >
/// URL Configuration), donde el frontend (pagina ResetPassword) captura la
/// sesion de invitacion y le pide al usuario que establezca su contrasena.
/// </summary>
public class SupabaseAuthAdminService : ISupabaseAuthAdminService
{
    private readonly HttpClient _http;

    public SupabaseAuthAdminService(HttpClient http, IOptions<SupabaseSettings> settings)
    {
        var s = settings.Value;
        _http = http;
        _http.BaseAddress = new Uri(s.Url);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.ServiceRoleKey);
        _http.DefaultRequestHeaders.Add("apikey", s.ServiceRoleKey);
    }

    public async Task<Guid> InviteUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/auth/v1/invite", new { email }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"No se pudo invitar al usuario en Supabase Auth: {response.StatusCode} - {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<SupabaseInviteResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Respuesta vacia de Supabase Auth al invitar al usuario.");

        return result.Id;
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/auth/v1/admin/users/{userId}", ct);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"No se pudo borrar al usuario en Supabase Auth: {response.StatusCode} - {body}");
        }
    }

    private record SupabaseInviteResponse([property: JsonPropertyName("id")] Guid Id);
}
