using FleetControl.Application.Common.Interfaces;

namespace FleetControl.WebAPI.IntegrationTests;

/// <summary>
/// Reemplazo en memoria de ISupabaseStorageService para pruebas de integracion:
/// el SupabaseStorageService real hace llamadas HTTP reales a la API de
/// Supabase Storage (y falla al construirse si Supabase:Url esta vacio), asi
/// que no es utilizable en un WebApplicationFactory sin credenciales reales.
/// </summary>
public class FakeSupabaseStorageService : ISupabaseStorageService
{
    public Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
        => Task.FromResult(path);

    public Task<string> GetSignedUrlAsync(string bucket, string path, int expiresInSeconds = 3600, CancellationToken ct = default)
        => Task.FromResult($"https://fake-storage.test/{bucket}/{path}?expires={expiresInSeconds}");

    public string GetPublicUrl(string bucket, string path) => $"https://fake-storage.test/public/{bucket}/{path}";

    public Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
        => Task.CompletedTask;
}
