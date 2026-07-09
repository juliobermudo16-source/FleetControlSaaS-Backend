using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.Services;
using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetControl.Infrastructure.BackgroundServices;

/// <summary>
/// Servicio en segundo plano (IHostedService/BackgroundService) que corre cada
/// N horas, recorre TODOS los tenants y vehiculos, calcula el semaforo de cada
/// mantenimiento/documento y envia un correo (Gmail SMTP) al administrador y al
/// conductor asignado cuando un item pasa a Amarillo o Rojo. Evita reenviar el
/// mismo aviso el mismo dia consultando notification_logs.
/// </summary>
public class MaintenanceAlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceAlertBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    public MaintenanceAlertBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MaintenanceAlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAndNotifyAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluando alertas de mantenimiento.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task EvaluateAndNotifyAsync(CancellationToken ct)
    {
        // Este servicio corre fuera del contexto de un usuario HTTP, por lo que
        // NO usa ICurrentUserService/query filters: consulta directamente el
        // DbContext "crudo" (sin filtro de tenant) para recorrer TODAS las
        // empresas, y filtra manualmente por cada TenantId.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.ApplicationDbContext>();
        var calculator = scope.ServiceProvider.GetRequiredService<IMaintenanceAlertCalculator>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var today = dateTime.Today;

        var vehicles = await db.Vehicles
            .IgnoreQueryFilters()
            .Include(v => v.Documents)
            .Include(v => v.MaintenanceLogs)
            .Include(v => v.AssignedDriver)
            .ToListAsync(ct);

        var maintenanceTypes = await db.MaintenanceTypes.IgnoreQueryFilters().ToListAsync(ct);

        foreach (var vehicle in vehicles)
        {
            var admin = await db.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == vehicle.TenantId && u.Role == UserRole.Admin && u.IsActive)
                .FirstOrDefaultAsync(ct);

            // --- Mantenimientos ---
            foreach (var type in maintenanceTypes.Where(t => t.TenantId == null || t.TenantId == vehicle.TenantId))
            {
                var lastMileage = vehicle.MaintenanceLogs
                    .Where(l => l.MaintenanceTypeId == type.Id)
                    .OrderByDescending(l => l.MileageAtService)
                    .Select(l => (int?)l.MileageAtService)
                    .FirstOrDefault() ?? 0;

                var status = calculator.CalculateMaintenanceStatus(
                    vehicle.Id, type.Id, type.Name, vehicle.CurrentMileage, lastMileage, type.IntervalKm);

                if (status.Status is AlertStatus.Yellow or AlertStatus.Red)
                {
                    var subject = $"[FleetControl] Alerta {status.Status} - {vehicle.LicensePlate} - {type.Name}";
                    var body = BuildMaintenanceEmailBody(vehicle, type.Name, status.WearPercentage, status.Status);

                    await NotifyOnceADayAsync(db, emailService, vehicle, admin, subject, body, status.Status, ct);
                }
            }

            // --- Documentos ---
            foreach (var doc in vehicle.Documents.Where(d => d.IsCurrent))
            {
                var status = calculator.CalculateDocumentStatus(vehicle.Id, doc.Id, doc.DocumentType, doc.ExpirationDate, today);

                if (status.Status is AlertStatus.Yellow or AlertStatus.Red)
                {
                    var subject = $"[FleetControl] Alerta {status.Status} - {vehicle.LicensePlate} - {doc.DocumentType}";
                    var body = BuildDocumentEmailBody(vehicle, doc.DocumentType.ToString(), status.DaysUntilExpiration, status.Status);

                    await NotifyOnceADayAsync(db, emailService, vehicle, admin, subject, body, status.Status, ct);
                }
            }
        }
    }

    private async Task NotifyOnceADayAsync(
        Persistence.ApplicationDbContext db,
        IEmailService emailService,
        Vehicle vehicle,
        AppUser? admin,
        string subject,
        string body,
        AlertStatus status,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var recipients = new List<string>();
        if (admin is not null) recipients.Add(admin.Email);
        if (vehicle.AssignedDriver is not null) recipients.Add(vehicle.AssignedDriver.Email);

        foreach (var recipient in recipients.Distinct())
        {
            var alreadySent = await db.NotificationLogs.IgnoreQueryFilters().AnyAsync(n =>
                n.VehicleId == vehicle.Id &&
                n.RecipientEmail == recipient &&
                n.Subject == subject &&
                n.SentAt.Date == today, ct);

            if (alreadySent) continue;

            try
            {
                await emailService.SendAsync(recipient, subject, body, ct);

                db.NotificationLogs.Add(new NotificationLog
                {
                    TenantId = vehicle.TenantId,
                    VehicleId = vehicle.Id,
                    RecipientEmail = recipient,
                    Subject = subject,
                    AlertStatus = status,
                    SentAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // No se interrumpe el ciclo de evaluacion de las demas alertas por
                // un fallo de envio individual, pero SI se registra en logs para
                // poder diagnosticar problemas de SMTP/credenciales.
                _logger.LogError(ex, "No se pudo enviar la alerta '{Subject}' a {Recipient}.", subject, recipient);
            }
        }
    }

    private static string BuildMaintenanceEmailBody(Vehicle v, string maintenanceName, double wearPercentage, AlertStatus status) =>
        $"""
        <h2>Alerta de mantenimiento - {status}</h2>
        <p>Vehiculo: <strong>{v.LicensePlate}</strong> ({v.Brand} {v.Model})</p>
        <p>Mantenimiento: <strong>{maintenanceName}</strong></p>
        <p>Desgaste actual: <strong>{wearPercentage:F1}%</strong></p>
        <p>Kilometraje actual: {v.CurrentMileage} km</p>
        """;

    private static string BuildDocumentEmailBody(Vehicle v, string documentType, int daysUntilExpiration, AlertStatus status) =>
        $"""
        <h2>Alerta de documento - {status}</h2>
        <p>Vehiculo: <strong>{v.LicensePlate}</strong> ({v.Brand} {v.Model})</p>
        <p>Documento: <strong>{documentType}</strong></p>
        <p>{(daysUntilExpiration <= 0 ? "Este documento ya se encuentra VENCIDO." : $"Dias restantes para vencer: {daysUntilExpiration}")}</p>
        """;
}
