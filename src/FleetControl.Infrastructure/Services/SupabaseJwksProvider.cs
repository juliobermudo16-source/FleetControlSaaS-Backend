using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FleetControl.Infrastructure.Services;

/// <summary>
/// Obtiene y cachea el JSON Web Key Set (JWKS) publico de Supabase Auth
/// (https://{proyecto}.supabase.co/auth/v1/.well-known/jwks.json).
///
/// Esto es necesario porque, desde 2025, los proyectos NUEVOS de Supabase
/// firman los JWT de sesion con una clave ASIMETRICA (ES256/RSA) por
/// defecto, en vez del antiguo "JWT Secret" compartido (HS256). El JWKS
/// contiene la(s) clave(s) publica(s) para verificar esos tokens localmente,
/// sin llamar al servidor de Supabase en cada request.
///
/// Si el proyecto de Supabase todavia usa el esquema legado (HS256 con
/// secreto compartido), este endpoint puede no devolver ninguna clave; en
/// ese caso el middleware cae de vuelta al "Legacy JWT Secret" configurado
/// en appsettings (ver SupabaseJwtMiddleware).
/// </summary>
public class SupabaseJwksProvider
{
    private readonly HttpClient _http;
    private readonly SupabaseSettings _settings;
    private JsonWebKeySet? _cachedKeySet;
    private DateTime _cachedAtUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10); // igual al cache edge de Supabase
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SupabaseJwksProvider(HttpClient http, IOptions<SupabaseSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct = default)
    {
        if (_cachedKeySet is not null && DateTime.UtcNow - _cachedAtUtc < CacheDuration)
            return ConvertKeys(_cachedKeySet);

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedKeySet is not null && DateTime.UtcNow - _cachedAtUtc < CacheDuration)
                return ConvertKeys(_cachedKeySet);

            var url = $"{_settings.Url.TrimEnd('/')}/auth/v1/.well-known/jwks.json";

            try
            {
                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                    return Array.Empty<SecurityKey>(); // proyecto probablemente en modo legacy (solo HS256)

                var json = await response.Content.ReadAsStringAsync(ct);
                _cachedKeySet = new JsonWebKeySet(json);
                _cachedAtUtc = DateTime.UtcNow;

                return ConvertKeys(_cachedKeySet);
            }
            catch
            {
                // Url invalida, sin conexion, timeout, etc. -> se cae al secreto
                // legado HS256 (si esta configurado) en SupabaseJwtMiddleware.
                return Array.Empty<SecurityKey>();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static IEnumerable<SecurityKey> ConvertKeys(JsonWebKeySet keySet) =>
        keySet.Keys.Select(k => (SecurityKey)k);
}
