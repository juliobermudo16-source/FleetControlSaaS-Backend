using FleetControl.Infrastructure.Identity;
using FleetControl.Infrastructure.Persistence;
using FleetControl.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace FleetControl.WebAPI.Middleware;

/// <summary>
/// Middleware personalizado (requisito del negocio) que:
///   1. Extrae el JWT del header "Authorization: Bearer {token}" emitido por Supabase Auth.
///   2. Valida su firma contra las claves de Supabase: primero intenta el
///      JWKS asimetrico (ES256/RSA, esquema actual de Supabase), y si el
///      proyecto no expone claves ahi, cae de vuelta al "Legacy JWT Secret"
///      compartido (HS256, esquema antiguo). Ver SupabaseJwksProvider.
///   3. Lee el claim "sub" (Guid del usuario en auth.users).
///   4. Busca el perfil de negocio en public.users para obtener TenantId y Role.
///   5. Puebla el CurrentUserService (Scoped) para que el resto del pipeline
///      (Controllers, Application services, DbContext) sepa "quien pregunta".
///
/// Rutas publicas (health check, swagger) se dejan pasar sin token.
/// </summary>
public class SupabaseJwtMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly string[] AnonymousPaths = { "/health", "/swagger" };

    public SupabaseJwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        CurrentUserService currentUserService,
        ApplicationDbContext db,
        IOptions<SupabaseSettings> supabaseSettings,
        SupabaseJwksProvider jwksProvider)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (AnonymousPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Falta el token de autenticacion (Authorization: Bearer)." });
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        var principal = await TryValidateTokenAsync(token, supabaseSettings.Value.JwtSecret, jwksProvider, context.RequestAborted);
        if (principal is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Token invalido o expirado." });
            return;
        }

        var userIdClaim = principal.FindFirst("sub")?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Token no contiene un 'sub' valido." });
            return;
        }

        // Se busca el perfil de negocio SIN filtro de tenant (aun no lo conocemos).
        var appUser = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
        if (appUser is null || !appUser.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Usuario no registrado o inactivo en el sistema." });
            return;
        }

        currentUserService.UserId = appUser.Id;
        currentUserService.TenantId = appUser.TenantId;
        currentUserService.Role = appUser.Role.ToString().ToLowerInvariant();
        currentUserService.Email = appUser.Email;

        await _next(context);
    }

    private static async Task<System.Security.Claims.ClaimsPrincipal?> TryValidateTokenAsync(
        string token, string legacySecret, SupabaseJwksProvider jwksProvider, CancellationToken ct)
    {
        try
        {
            // MapInboundClaims = false: por defecto JwtSecurityTokenHandler
            // remapea claims cortos (ej. "sub") a URIs largas de ClaimTypes
            // (ClaimTypes.NameIdentifier), lo que rompe el FindFirst("sub")
            // de mas abajo tanto para tokens de prueba como para los reales
            // de Supabase.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var jwksKeys = (await jwksProvider.GetSigningKeysAsync(ct)).ToList();

            // Se combinan las claves asimetricas (JWKS) con el secreto legado
            // HS256 (si esta configurado), y se deja que la validacion pruebe
            // la que corresponda segun el "kid" o algoritmo del token.
            var candidateKeys = new List<SecurityKey>(jwksKeys);
            if (!string.IsNullOrWhiteSpace(legacySecret))
                candidateKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(legacySecret)));

            if (candidateKeys.Count == 0)
                return null; // no hay ninguna clave configurada / disponible

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = candidateKeys,
                ValidateIssuer = false,   // Supabase incluye el issuer del proyecto; se puede activar si se desea ser estricto
                ValidateAudience = false, // Supabase usa "authenticated" como audience
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch
        {
            return null;
        }
    }
}

public static class SupabaseJwtMiddlewareExtensions
{
    public static IApplicationBuilder UseSupabaseJwtAuthentication(this IApplicationBuilder app)
        => app.UseMiddleware<SupabaseJwtMiddleware>();
}
