using FleetControl.Application.DTOs;
using FleetControl.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetControl.WebAPI.Controllers;

/// <summary>Carga y consulta de fotos de vehiculos (bucket publico).</summary>
public class PhotosController : BaseApiController
{
    private readonly IVehiclePhotoService _photoService;

    public PhotosController(IVehiclePhotoService photoService)
    {
        _photoService = photoService;
    }

    /// <summary>Sube una foto. multipart/form-data: file, vehicleId, isPrimary.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)] // 10 MB
    public async Task<ActionResult<VehiclePhotoDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] Guid vehicleId,
        [FromForm] bool isPrimary,
        CancellationToken ct)
    {
        if (!file.ContentType.StartsWith("image/"))
            return BadRequest(new { error = "Solo se permiten archivos de imagen." });

        await using var stream = file.OpenReadStream();
        var dto = new UploadPhotoDto(vehicleId, isPrimary);
        var result = await _photoService.UploadAsync(dto, stream, file.FileName, ct);
        return Ok(result);
    }

    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<ActionResult<IReadOnlyList<VehiclePhotoDto>>> GetByVehicle(Guid vehicleId, CancellationToken ct)
        => Ok(await _photoService.GetByVehicleAsync(vehicleId, ct));

    [HttpDelete("{photoId:guid}")]
    public async Task<IActionResult> Delete(Guid photoId, CancellationToken ct)
    {
        await _photoService.DeleteAsync(photoId, ct);
        return NoContent();
    }
}
