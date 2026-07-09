using FleetControl.Application.DTOs;
using FleetControl.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetControl.WebAPI.Controllers;

/// <summary>Perfil del usuario actual y gestion de usuarios del tenant (invitaciones, solo Admin).</summary>
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken ct)
        => Ok(await _userService.GetCurrentUserAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct)
        => Ok(await _userService.GetTenantUsersAsync(ct));

    [HttpPost("invite")]
    public async Task<ActionResult<UserDto>> Invite([FromBody] InviteUserDto dto, CancellationToken ct)
        => Ok(await _userService.InviteUserAsync(dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _userService.DeactivateUserAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await _userService.ReactivateUserAsync(id, ct);
        return NoContent();
    }
}
