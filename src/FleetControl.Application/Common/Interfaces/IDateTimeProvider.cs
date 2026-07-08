namespace FleetControl.Application.Common.Interfaces;

/// <summary>Abstrae DateTime.UtcNow para que la logica de alertas sea 100% testeable.</summary>
public interface IDateTimeProvider
{
    DateOnly Today { get; }
    DateTime UtcNow { get; }
}
