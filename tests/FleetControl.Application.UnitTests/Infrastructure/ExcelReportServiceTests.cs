using FleetControl.Application.Common.Interfaces;
using FleetControl.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace FleetControl.Application.UnitTests.Infrastructure;

public class ExcelReportServiceTests
{
    private readonly ExcelReportService _sut = new();

    [Fact]
    public void GenerateFleetExcel_DebeProducirUnArchivoXlsxValido_ConVehiculos()
    {
        var data = new FleetReportData("Transportes Ayacucho SAC", DateTime.UtcNow, new List<VehicleReportRow>
        {
            new("AAA-111", "Toyota Hilux", 12000, "Conductor Uno", "Green", 0m),
            new("BBB-222", "Kia Rio", 8000, "Sin asignar", "Yellow", 80m)
        });

        var bytes = _sut.GenerateFleetExcel(data);

        bytes.Should().NotBeEmpty();
        // Los archivos .xlsx son un ZIP: firma "PK".
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }

    [Fact]
    public void GenerateFleetExcel_DebeProducirUnArchivoValido_SinVehiculos()
    {
        var data = new FleetReportData("Empresa Vacia", DateTime.UtcNow, new List<VehicleReportRow>());

        var bytes = _sut.GenerateFleetExcel(data);

        bytes.Should().NotBeEmpty();
    }
}
