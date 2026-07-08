namespace FleetControl.Application.Common.Interfaces;

public interface IExcelReportService
{
    /// <summary>Genera el listado detallado de flota en Excel (EPPlus) y devuelve los bytes.</summary>
    byte[] GenerateFleetExcel(FleetReportData data);
}
