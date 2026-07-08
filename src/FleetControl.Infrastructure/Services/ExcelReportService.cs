using FleetControl.Application.Common.Interfaces;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace FleetControl.Infrastructure.Services;

/// <summary>Genera el listado detallado de flota en Excel usando EPPlus (licencia NonCommercial).</summary>
public class ExcelReportService : IExcelReportService
{
    public ExcelReportService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public byte[] GenerateFleetExcel(FleetReportData data)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Flota");

        string[] headers = { "Placa", "Marca / Modelo", "Km actual", "Conductor", "Estado", "Costo estimado (S/)" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
        }

        int row = 2;
        foreach (var v in data.Vehicles)
        {
            sheet.Cells[row, 1].Value = v.LicensePlate;
            sheet.Cells[row, 2].Value = v.BrandModel;
            sheet.Cells[row, 3].Value = v.CurrentMileage;
            sheet.Cells[row, 4].Value = v.DriverName;
            sheet.Cells[row, 5].Value = v.OverallStatus;
            sheet.Cells[row, 6].Value = v.EstimatedMaintenanceCost;
            row++;
        }

        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        return package.GetAsByteArray();
    }
}
