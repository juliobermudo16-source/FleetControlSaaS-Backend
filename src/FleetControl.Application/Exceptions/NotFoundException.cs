namespace FleetControl.Application.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"'{entity}' con id '{key}' no fue encontrado.") { }
}
