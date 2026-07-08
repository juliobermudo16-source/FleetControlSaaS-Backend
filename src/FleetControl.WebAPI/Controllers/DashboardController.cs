using FleetControl.Application.DTOs;
using FleetControl.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetControl.WebAPI.Controllers;

/// <summary>Resumen ejecutivo para el Dashboard del Administrador.</summary>
public class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        RequireAdmin();
        return Ok(await _dashboardService.GetSummaryAsync(ct));
    }
}
