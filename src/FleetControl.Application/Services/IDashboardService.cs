using FleetControl.Application.DTOs;

namespace FleetControl.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}
