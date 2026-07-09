using FleetControl.Application.Common.Interfaces;
using FleetControl.Infrastructure.BackgroundServices;
using FleetControl.Infrastructure.Identity;
using FleetControl.Infrastructure.Persistence;
using FleetControl.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FleetControl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment = null)
    {
        // --- Base de datos (Postgres en prod, InMemory en tests) ---
        var connectionString = configuration.GetConnectionString("SupabaseConnection");
        if (environment is not null && environment.IsEnvironment("Testing"))
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("FleetControl.Tests.InMemory"));
        }
        else
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));
        }
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // --- Identidad del usuario actual (poblada por el middleware JWT) ---
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(p => p.GetRequiredService<CurrentUserService>());

        // --- Configuracion tipada ---
        services.Configure<SupabaseSettings>(configuration.GetSection("Supabase"));
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));

        // --- Servicios de infraestructura ---
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddHttpClient<IEmailService, EmailService>();
        services.AddScoped<IPdfReportService, PdfReportService>();
        services.AddScoped<IExcelReportService, ExcelReportService>();
        services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>();
        services.AddHttpClient<ISupabaseAuthAdminService, SupabaseAuthAdminService>();
        services.AddHttpClient<SupabaseJwksProvider>();

        // --- Background Services ---
        services.AddHostedService<MaintenanceAlertBackgroundService>();
        services.AddHostedService<UserDeletionBackgroundService>();

        return services;
    }
}
