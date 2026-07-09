using FleetControl.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetControl.Infrastructure.BackgroundServices;

/// <summary>
/// Recorre periodicamente TODOS los tenants buscando usuarios con un borrado
/// programado (AppUser.PendingDeletionAt) ya vencido, y los elimina de forma
/// permanente: primero de Supabase Auth (ya no podran iniciar sesion) y luego
/// su fila en public.users. Mientras el admin no cancele la eliminacion (ver
/// UserService.ReactivateUserAsync) dentro de la ventana de gracia, el borrado
/// es irreversible.
/// </summary>
public class UserDeletionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserDeletionBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public UserDeletionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<UserDeletionBackgroundService> logger)
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
                await ProcessDueDeletionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando borrados de usuarios programados.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ProcessDueDeletionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.ApplicationDbContext>();
        var authAdmin = scope.ServiceProvider.GetRequiredService<ISupabaseAuthAdminService>();
        var storage = scope.ServiceProvider.GetRequiredService<ISupabaseStorageService>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var now = dateTime.UtcNow;

        var dueUsers = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.PendingDeletionAt != null && u.PendingDeletionAt <= now)
            .ToListAsync(ct);

        foreach (var user in dueUsers)
        {
            try
            {
                await authAdmin.DeleteUserAsync(user.Id, ct);

                if (!string.IsNullOrEmpty(user.AvatarStoragePath))
                    await storage.DeleteAsync("user-avatars", user.AvatarStoragePath, ct);

                db.Users.Remove(user);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Se deja PendingDeletionAt intacto para reintentar en el
                // siguiente ciclo; no se interrumpe el borrado de los demas.
                _logger.LogError(ex, "No se pudo borrar permanentemente al usuario {UserId}.", user.Id);
            }
        }
    }
}
