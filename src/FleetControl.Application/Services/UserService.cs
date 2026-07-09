using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.DTOs;
using FleetControl.Application.Exceptions;
using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetControl.Application.Services;

/// <summary>
/// Gestiona el perfil del usuario actual y la invitacion de nuevos usuarios
/// por parte de un Administrador. La invitacion crea el registro en Supabase
/// Auth (via ISupabaseAuthAdminService) y, con el mismo Id (Guid), el perfil
/// de negocio en public.users dentro del tenant del admin que invita.
/// </summary>
public class UserService : IUserService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISupabaseAuthAdminService _authAdmin;

    public UserService(IApplicationDbContext db, ICurrentUserService currentUser, ISupabaseAuthAdminService authAdmin)
    {
        _db = db;
        _currentUser = currentUser;
        _authAdmin = authAdmin;
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(AppUser), _currentUser.UserId);

        return MapToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetTenantUsersAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede ver la lista de usuarios.");

        var users = await _db.Users.OrderBy(u => u.FullName).ToListAsync(ct);
        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto> InviteUserAsync(InviteUserDto dto, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede invitar usuarios.");

        if (dto.Role is not ("admin" or "driver"))
            throw new InvalidOperationException("El rol debe ser 'admin' o 'driver'.");

        var alreadyInTenant = await _db.Users.AnyAsync(u => u.Email == dto.Email, ct);
        if (alreadyInTenant)
            throw new InvalidOperationException("Ya existe un usuario con ese correo en esta empresa.");

        var newUserId = await _authAdmin.InviteUserByEmailAsync(dto.Email, ct);

        var user = new AppUser
        {
            Id = newUserId,
            TenantId = _currentUser.TenantId,
            FullName = dto.FullName,
            Email = dto.Email,
            Role = dto.Role == "admin" ? UserRole.Admin : UserRole.Driver,
            Phone = dto.Phone,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return MapToDto(user);
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede eliminar usuarios.");

        if (userId == _currentUser.UserId)
            throw new InvalidOperationException("No puedes eliminarte a ti mismo.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(AppUser), userId);

        user.IsActive = false;

        // Se desasigna de cualquier vehiculo para que no quede "en manos" de un
        // conductor sin acceso al sistema.
        var assignedVehicles = await _db.Vehicles.Where(v => v.AssignedDriverId == userId).ToListAsync(ct);
        foreach (var vehicle in assignedVehicles)
            vehicle.AssignedDriverId = null;

        await _db.SaveChangesAsync(ct);
    }

    public async Task ReactivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin)
            throw new ForbiddenAccessException("Solo un administrador puede reactivar usuarios.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(AppUser), userId);

        user.IsActive = true;
        await _db.SaveChangesAsync(ct);
    }

    private static UserDto MapToDto(AppUser u) =>
        new(u.Id, u.FullName, u.Email, u.Role.ToString().ToLowerInvariant(), u.IsActive, u.Phone);
}
