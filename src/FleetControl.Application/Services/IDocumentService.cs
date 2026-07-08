using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IDocumentService
{
    Task<DocumentDto> UploadAsync(UploadDocumentDto dto, Stream fileContent, string fileName, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDto>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<string> GetDownloadUrlAsync(Guid documentId, CancellationToken ct = default);
}
