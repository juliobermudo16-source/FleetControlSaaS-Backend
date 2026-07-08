using FleetControl.Application.Common.Interfaces;
using FleetControl.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FleetControl.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    private ICurrentUserService? _currentUser;
    protected ICurrentUserService CurrentUser => _currentUser ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

    /// <summary>Lanza 403 si el usuario actual no es Administrador.</summary>
    protected void RequireAdmin()
    {
        if (!CurrentUser.IsAdmin)
            throw new ForbiddenAccessException("Esta accion requiere rol de Administrador.");
    }
}
