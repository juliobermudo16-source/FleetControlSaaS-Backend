using FleetControl.Domain.Common;
using FleetControl.Domain.Enums;

namespace FleetControl.Domain.Entities;

public class VehicleDocument : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string StoragePath { get; set; } = string.Empty; // bucket privado 'vehicle-documents'
    public string FileHashSha256 { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public Guid? UploadedBy { get; set; }

    /// <summary>
    /// true = es el documento vigente de su tipo para el vehiculo (el que se
    /// muestra por defecto). Al subir un documento nuevo del mismo tipo, el
    /// anterior pasa a false en vez de borrarse, para conservar el historial.
    /// </summary>
    public bool IsCurrent { get; set; } = true;

    public Vehicle? Vehicle { get; set; }

    /// <summary>Dias restantes hasta el vencimiento (negativo si ya vencio).</summary>
    public int DaysUntilExpiration(DateOnly today) => ExpirationDate.DayNumber - today.DayNumber;
}
