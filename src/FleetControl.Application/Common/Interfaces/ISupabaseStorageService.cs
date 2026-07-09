namespace FleetControl.Application.Common.Interfaces;

public interface ISupabaseStorageService
{
    /// <summary>Sube un archivo y devuelve el storage_path guardado en la BD.</summary>
    Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Genera una URL firmada temporal para descargar un archivo de un bucket privado.</summary>
    Task<string> GetSignedUrlAsync(string bucket, string path, int expiresInSeconds = 3600, CancellationToken ct = default);

    /// <summary>URL publica permanente (sin firmar) para un archivo de un bucket publico, ej. vehicle-photos.</summary>
    string GetPublicUrl(string bucket, string path);

    Task DeleteAsync(string bucket, string path, CancellationToken ct = default);
}
