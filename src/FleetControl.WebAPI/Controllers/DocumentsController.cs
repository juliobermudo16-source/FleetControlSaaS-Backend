using FleetControl.Application.DTOs;
using FleetControl.Application.Services;
using FleetControl.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetControl.WebAPI.Controllers;

/// <summary>Carga y consulta de documentos obligatorios (SOAT, Revision Tecnica, Tarjeta de Propiedad).</summary>
public class DocumentsController : BaseApiController
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>Sube un PDF. multipart/form-data: file, vehicleId, documentType, issueDate, expirationDate.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)] // 10 MB
    public async Task<ActionResult<DocumentDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] Guid vehicleId,
        [FromForm] DocumentType documentType,
        [FromForm] DateOnly issueDate,
        [FromForm] DateOnly expirationDate,
        CancellationToken ct)
    {
        if (file.ContentType != "application/pdf")
            return BadRequest(new { error = "Solo se permiten archivos PDF." });

        await using var stream = file.OpenReadStream();
        var dto = new UploadDocumentDto(vehicleId, documentType, issueDate, expirationDate);
        var result = await _documentService.UploadAsync(dto, stream, file.FileName, ct);
        return Ok(result);
    }

    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> GetByVehicle(Guid vehicleId, CancellationToken ct)
        => Ok(await _documentService.GetByVehicleAsync(vehicleId, ct));

    /// <summary>Devuelve TODOS los documentos (vigente + historicos) de un tipo para un vehiculo, mas recientes primero.</summary>
    [HttpGet("vehicle/{vehicleId:guid}/type/{documentType}/history")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> GetHistory(Guid vehicleId, DocumentType documentType, CancellationToken ct)
        => Ok(await _documentService.GetHistoryByTypeAsync(vehicleId, documentType, ct));

    [HttpGet("{documentId:guid}/download-url")]
    public async Task<ActionResult<string>> GetDownloadUrl(Guid documentId, CancellationToken ct)
        => Ok(new { url = await _documentService.GetDownloadUrlAsync(documentId, ct) });

    [HttpPut("{documentId:guid}")]
    public async Task<ActionResult<DocumentDto>> UpdateDates(Guid documentId, [FromBody] UpdateDocumentDatesDto dto, CancellationToken ct)
        => Ok(await _documentService.UpdateDatesAsync(documentId, dto.IssueDate, dto.ExpirationDate, ct));

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken ct)
    {
        await _documentService.DeleteAsync(documentId, ct);
        return NoContent();
    }
}
