using System.Text;
using FleetControl.Application.Common.Interfaces;
using FleetControl.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace FleetControl.Application.UnitTests.Infrastructure;

public class PdfReportServiceTests
{
    private readonly PdfReportService _sut = new();

    [Fact]
    public void GenerateFleetReport_DebeProducirUnPdfValido_ConVehiculos()
    {
        var data = new FleetReportData("Transportes Ayacucho SAC", DateTime.UtcNow, new List<VehicleReportRow>
        {
            new("AAA-111", "Toyota Hilux", 12000, "Conductor Uno", "Green", 0m),
            new("BBB-222", "Kia Rio", 8000, "Sin asignar", "Red", 150m)
        });

        var bytes = _sut.GenerateFleetReport(data);

        bytes.Should().NotBeEmpty();
        // Los archivos PDF comienzan con la firma "%PDF".
        Encoding.UTF8.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void GenerateFleetReport_DebeProducirUnPdfValido_SinVehiculos()
    {
        var data = new FleetReportData("Empresa Vacia", DateTime.UtcNow, new List<VehicleReportRow>());

        var bytes = _sut.GenerateFleetReport(data);

        bytes.Should().NotBeEmpty();
    }
}
