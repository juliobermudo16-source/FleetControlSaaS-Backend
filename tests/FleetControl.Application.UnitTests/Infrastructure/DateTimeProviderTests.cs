using FleetControl.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace FleetControl.Application.UnitTests.Infrastructure;

public class DateTimeProviderTests
{
    [Fact]
    public void Today_DebeRetornarLaFechaActualEnUtc()
    {
        var sut = new DateTimeProvider();

        sut.Today.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public void UtcNow_DebeEstarCercaDeLaHoraActual()
    {
        var sut = new DateTimeProvider();

        sut.UtcNow.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
