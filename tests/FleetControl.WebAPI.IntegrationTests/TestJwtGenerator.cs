using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FleetControl.WebAPI.IntegrationTests;

/// <summary>Genera JWTs firmados con el mismo secreto de prueba que usa CustomWebApplicationFactory, simulando los tokens que emitiria Supabase Auth.</summary>
public static class TestJwtGenerator
{
    public const string TestSecret = "esto-es-un-secreto-de-prueba-de-al-menos-32-caracteres!!";

    public static string GenerateToken(Guid userId) => GenerateToken(userId.ToString(), TestSecret);

    /// <summary>Firma con un secreto DISTINTO al configurado, para simular un token invalido/manipulado.</summary>
    public static string GenerateTokenWithWrongSecret(Guid userId) =>
        GenerateToken(userId.ToString(), "otro-secreto-completamente-diferente-de-al-menos-32-caracteres");

    /// <summary>Genera un token cuyo claim "sub" no es un Guid valido.</summary>
    public static string GenerateTokenWithInvalidSub() => GenerateToken("no-soy-un-guid", TestSecret);

    /// <summary>Genera un token ya vencido.</summary>
    public static string GenerateExpiredToken(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim("sub", userId.ToString()) };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateToken(string sub, string secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[] { new Claim("sub", sub) };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
