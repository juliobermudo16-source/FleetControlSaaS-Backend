using FleetControl.Application.DTOs;
using FleetControl.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetControl.WebAPI.Controllers;

/// <summary>
/// CRUD de vehiculos. Admin ve/gestiona toda la flota de su tenant;
/// Conductor solo ve y reporta su vehiculo asignado (la logica de filtrado
/// vive en VehicleService, no aqui).
/// </summary>
public class VehiclesController : BaseApiController
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetAll(CancellationToken ct)
        => Ok(await _vehicleService.GetVehiclesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _vehicleService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> Create([FromBody] CreateVehicleDto dto, CancellationToken ct)
    {
        var created = await _vehicleService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VehicleDto>> Update(Guid id, [FromBody] UpdateVehicleDto dto, CancellationToken ct)
        => Ok(await _vehicleService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _vehicleService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>El conductor reporta el kilometraje actual de su vehiculo.</summary>
    [HttpPost("{id:guid}/report-mileage")]
    public async Task<ActionResult<VehicleDto>> ReportMileage(Guid id, [FromBody] ReportMileageDto dto, CancellationToken ct)
        => Ok(await _vehicleService.ReportMileageAsync(id, dto, ct));
}
