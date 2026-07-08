namespace FleetControl.Application.Exceptions;

/// <summary>Se lanza cuando un usuario intenta acceder a un recurso fuera de su Tenant o rol.</summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "No tiene permisos para realizar esta accion.")
        : base(message) { }
}
