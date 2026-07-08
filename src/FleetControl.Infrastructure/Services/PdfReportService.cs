using FleetControl.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FleetControl.Infrastructure.Services;

/// <summary>Genera el PDF consolidado de flota usando QuestPDF (licencia Community).</summary>
public class PdfReportService : IPdfReportService
{
    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateFleetReport(FleetReportData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(data.CompanyName).FontSize(18).Bold();
                    col.Item().Text($"Reporte consolidado de flota - Generado: {data.GeneratedAt:dd/MM/yyyy HH:mm}").FontSize(9);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.2f); // Placa
                        columns.RelativeColumn(2);    // Marca/Modelo
                        columns.RelativeColumn(1.3f);   // Km
                        columns.RelativeColumn(2);      // Conductor
                        columns.RelativeColumn(1.3f);   // Estado
                        columns.RelativeColumn(1.5f);   // Costo estimado
                    });

                    table.Header(header =>
                    {
                        foreach (var title in new[] { "Placa", "Marca / Modelo", "Km actual", "Conductor", "Estado", "Costo est." })
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(title).Bold();
                    });

                    foreach (var v in data.Vehicles)
                    {
                        table.Cell().Padding(4).Text(v.LicensePlate);
                        table.Cell().Padding(4).Text(v.BrandModel);
                        table.Cell().Padding(4).Text($"{v.CurrentMileage} km");
                        table.Cell().Padding(4).Text(v.DriverName);
                        table.Cell().Padding(4).Text(v.OverallStatus);
                        table.Cell().Padding(4).Text($"S/ {v.EstimatedMaintenanceCost:F2}");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("FleetControl SaaS");
                    x.Span(" - Documento generado automaticamente");
                });
            });
        });

        return document.GeneratePdf();
    }
}
