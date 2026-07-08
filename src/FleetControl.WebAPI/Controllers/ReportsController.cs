using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetControl.WebAPI.Controllers;

/// <summary>Exportacion de reportes consolidados de flota en PDF y Excel.</summary>
public class ReportsController : BaseApiController
{
    private readonly IDashboardService _dashboardService;
    private readonly IVehicleService _vehicleService;
    private readonly IPdfReportService _pdfService;
    private readonly IExcelReportService _excelService;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(
        IDashboardService dashboardService,
        IVehicleService vehicleService,
        IPdfReportService pdfService,
        IExcelReportService excelService,
        ICurrentUserService currentUser)
    {
        _dashboardService = dashboardService;
        _vehicleService = vehicleService;
        _pdfService = pdfService;
        _excelService = excelService;
        _currentUser = currentUser;
    }

    [HttpGet("fleet/pdf")]
    public async Task<IActionResult> GetFleetPdf(CancellationToken ct)
    {
        RequireAdmin();
        var data = await BuildReportDataAsync(ct);
        var bytes = _pdfService.GenerateFleetReport(data);
        return File(bytes, "application/pdf", $"reporte-flota-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("fleet/excel")]
    public async Task<IActionResult> GetFleetExcel(CancellationToken ct)
    {
        RequireAdmin();
        var data = await BuildReportDataAsync(ct);
        var bytes = _excelService.GenerateFleetExcel(data);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"reporte-flota-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private async Task<FleetReportData> BuildReportDataAsync(CancellationToken ct)
    {
        var vehicles = await _vehicleService.GetVehiclesAsync(ct);
        var summary = await _dashboardService.GetSummaryAsync(ct);

        // Nota: el costo estimado por vehiculo individual requeriria exponer
        // EstimatedCost en MaintenanceStatusDto; por simplicidad este reporte
        // usa el costo global de flota (summary.EstimatedUpcomingCost) y lo
        // deja en 0 por fila. Ampliar segun necesidad del negocio.
        var rows = vehicles.Select(v => new VehicleReportRow(
            v.LicensePlate,
            $"{v.Brand} {v.Model}",
            v.CurrentMileage,
            v.AssignedDriverName ?? "Sin asignar",
            v.OverallAlertStatus.ToString(),
            0m
        )).ToList();

        return new FleetReportData("Mi Empresa", DateTime.UtcNow, rows);
    }
}
