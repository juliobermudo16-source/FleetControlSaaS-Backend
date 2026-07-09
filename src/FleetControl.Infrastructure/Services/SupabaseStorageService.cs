using FleetControl.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace FleetControl.Infrastructure.Services;

/// <summary>
/// Cliente HTTP delgado hacia la API REST de Supabase Storage. Se usa la
/// service_role key (nunca expuesta al frontend) para poder escribir en el
/// bucket privado 'vehicle-documents' saltandose las policies de Storage,
/// ya que la autorizacion real ya fue validada en el Controller/Middleware.
/// </summary>
public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _http;
    private readonly SupabaseSettings _settings;

    public SupabaseStorageService(HttpClient http, IOptions<SupabaseSettings> settings)
    {
        _settings = settings.Value;
        _http = http;
        _http.BaseAddress = new Uri(_settings.Url);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
        _http.DefaultRequestHeaders.Add("apikey", _settings.ServiceRoleKey);
    }

    public async Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await _http.PostAsync($"/storage/v1/object/{bucket}/{path}", streamContent, ct);
        response.EnsureSuccessStatusCode();

        return path;
    }

    public async Task<string> GetSignedUrlAsync(string bucket, string path, int expiresInSeconds = 3600, CancellationToken ct = default)
    {
        var body = new StringContent($"{{\"expiresIn\":{expiresInSeconds}}}", System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"/storage/v1/object/sign/{bucket}/{path}", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var signedUrl = doc.RootElement.GetProperty("signedURL").GetString() ?? string.Empty;

        return $"{_settings.Url}/storage/v1{signedUrl}";
    }

    public string GetPublicUrl(string bucket, string path) =>
        $"{_settings.Url}/storage/v1/object/public/{bucket}/{path}";

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/storage/v1/object/{bucket}/{path}", ct);
        response.EnsureSuccessStatusCode();
    }
}
