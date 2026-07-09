using FleetControl.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FleetControl.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IMaintenanceAlertCalculator, MaintenanceAlertCalculator>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IVehiclePhotoService, VehiclePhotoService>();

        return services;
    }
}
