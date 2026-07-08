using FleetControl.Application.Services;
using FleetControl.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FleetControl.Application.UnitTests.Services;

/// <summary>
/// Pruebas unitarias del calculo de semaforo de MANTENIMIENTOS.
/// Formula: % Desgaste = ((KmActual - KmUltimoMantenimiento) / IntervaloKm) * 100
/// Verde &lt;= 80% | Amarillo &gt; 80% y &lt;= 100% | Rojo &gt; 100%
/// </summary>
public class MaintenanceAlertCalculatorTests
{
    private readonly MaintenanceAlertCalculator _sut = new(); // Sujeto bajo prueba

    [Theory]
    [InlineData(0, 0, 5000, 0)]        // recien hecho -> 0% desgaste
    [InlineData(2000, 0, 5000, 40)]    // 2000/5000 = 40%
    [InlineData(4000, 0, 5000, 80)]    // exactamente en el limite verde/amarillo
    public void CalculateMaintenanceStatus_DebeSerVerde_CuandoDesgasteEsMenorOIgualA80Porciento(
        int currentMileage, int lastServiceMileage, int intervalKm, double expectedWear)
    {
        var result = _sut.CalculateMaintenanceStatus(
            Guid.NewGuid(), Guid.NewGuid(), "Cambio de aceite", currentMileage, lastServiceMileage, intervalKm);

        result.WearPercentage.Should().Be(expectedWear);
        result.Status.Should().Be(AlertStatus.Green);
    }

    [Theory]
    [InlineData(4001, 0, 5000)]   // 80.02% -> justo por encima del umbral
    [InlineData(4500, 0, 5000)]   // 90%
    [InlineData(5000, 0, 5000)]   // exactamente 100% -> segun regla, Rojo es ">100", 100% cae en Amarillo
    public void CalculateMaintenanceStatus_DebeSerAmarillo_CuandoDesgasteEntre80y100Porciento(
        int currentMileage, int lastServiceMileage, int intervalKm)
    {
        var result = _sut.CalculateMaintenanceStatus(
            Guid.NewGuid(), Guid.NewGuid(), "Cambio de pastillas de freno", currentMileage, lastServiceMileage, intervalKm);

        result.Status.Should().Be(AlertStatus.Yellow);
    }

    [Theory]
    [InlineData(5001, 0, 5000)]   // 100.02%
    [InlineData(10000, 0, 5000)]  // 200%
    public void CalculateMaintenanceStatus_DebeSerRojo_CuandoDesgasteSuperaA100Porciento(
        int currentMileage, int lastServiceMileage, int intervalKm)
    {
        var result = _sut.CalculateMaintenanceStatus(
            Guid.NewGuid(), Guid.NewGuid(), "Mantenimiento general", currentMileage, lastServiceMileage, intervalKm);

        result.Status.Should().Be(AlertStatus.Red);
    }

    [Fact]
    public void CalculateMaintenanceStatus_DebeUsarUltimoKilometrajeDeServicio_NoDesdeCero()
    {
        // Vehiculo con 52,000 km, ultimo cambio de aceite a los 50,000 km, intervalo 5,000 km
        // -> recorrio 2,000 km desde el ultimo servicio -> 40% de desgaste (Verde), NO 1040%.
        var result = _sut.CalculateMaintenanceStatus(
            Guid.NewGuid(), Guid.NewGuid(), "Cambio de aceite",
            currentMileage: 52000, lastServiceMileage: 50000, intervalKm: 5000);

        result.WearPercentage.Should().Be(40);
        result.Status.Should().Be(AlertStatus.Green);
    }

    [Fact]
    public void CalculateMaintenanceStatus_NoDebeGenerarDesgasteNegativo_SiKmActualEsMenorAlUltimoServicio()
    {
        // Caso borde: correccion de odometro o dato inconsistente.
        var result = _sut.CalculateMaintenanceStatus(
            Guid.NewGuid(), Guid.NewGuid(), "Cambio de aceite",
            currentMileage: 1000, lastServiceMileage: 5000, intervalKm: 5000);

        result.WearPercentage.Should().Be(0);
        result.Status.Should().Be(AlertStatus.Green);
    }

    [Fact]
    public void CalculateMaintenanceStatus_DebeLanzarExcepcion_SiIntervaloKmEsCeroONegativo()
    {
        var act = () => _sut.CalculateMaintenanceStatus(Guid.NewGuid(), Guid.NewGuid(), "X", 1000, 0, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------------- Documentos (SOAT, Revision Tecnica, Tarjeta de Propiedad) ----------------

    [Fact]
    public void CalculateDocumentStatus_DebeSerVerde_CuandoFaltanMasDe30Dias()
    {
        var today = new DateOnly(2026, 1, 1);
        var expiration = today.AddDays(45);

        var result = _sut.CalculateDocumentStatus(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Soat, expiration, today);

        result.Status.Should().Be(AlertStatus.Green);
        result.DaysUntilExpiration.Should().Be(45);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(15)]
    [InlineData(1)]
    public void CalculateDocumentStatus_DebeSerAmarillo_CuandoFaltan30DiasOMenos(int daysRemaining)
    {
        var today = new DateOnly(2026, 1, 1);
        var expiration = today.AddDays(daysRemaining);

        var result = _sut.CalculateDocumentStatus(Guid.NewGuid(), Guid.NewGuid(), DocumentType.RevisionTecnica, expiration, today);

        result.Status.Should().Be(AlertStatus.Yellow);
    }

    [Theory]
    [InlineData(0)]   // vence hoy
    [InlineData(-10)] // ya vencido hace 10 dias
    public void CalculateDocumentStatus_DebeSerRojo_CuandoEstaVencidoOVenceHoy(int daysRemaining)
    {
        var today = new DateOnly(2026, 1, 1);
        var expiration = today.AddDays(daysRemaining);

        var result = _sut.CalculateDocumentStatus(Guid.NewGuid(), Guid.NewGuid(), DocumentType.TarjetaPropiedad, expiration, today);

        result.Status.Should().Be(AlertStatus.Red);
    }
}
