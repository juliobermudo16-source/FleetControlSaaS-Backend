using FleetControl.Application.DTOs;
using FleetControl.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetControl.WebAPI.Controllers;

public class MaintenanceController : BaseApiController
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    /// <summary>Registra un mantenimiento realizado (Admin, o el conductor asignado una vez por avance de kilometraje).</summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceLogDto>> Register([FromBody] CreateMaintenanceLogDto dto, CancellationToken ct)
        => Ok(await _maintenanceService.RegisterMaintenanceAsync(dto, ct));

    /// <summary>Devuelve el semaforo de cada tipo de mantenimiento para un vehiculo.</summary>
    [HttpGet("vehicle/{vehicleId:guid}/status")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceStatusDto>>> GetStatus(Guid vehicleId, CancellationToken ct)
        => Ok(await _maintenanceService.GetVehicleMaintenanceStatusAsync(vehicleId, ct));

    /// <summary>Devuelve el historial completo de mantenimientos registrados para un vehiculo.</summary>
    [HttpGet("vehicle/{vehicleId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceLogDto>>> GetHistory(Guid vehicleId, CancellationToken ct)
        => Ok(await _maintenanceService.GetVehicleMaintenanceHistoryAsync(vehicleId, ct));
}
