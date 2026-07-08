namespace FleetControl.Domain.Enums;

/// <summary>
/// Semaforo de estado usado tanto para mantenimientos (por % de desgaste)
/// como para documentos (por dias para vencer / vencido).
/// </summary>
public enum AlertStatus
{
    Green = 0,
    Yellow = 1,
    Red = 2
}
