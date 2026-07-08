using FleetControl.Application.Common.Interfaces;
using FleetControl.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace FleetControl.Application.UnitTests.Infrastructure;

public class CurrentUserServiceTests
{
    [Fact]
    public void IsAdmin_DebeSerFalso_PorDefecto()
    {
        ICurrentUserService sut = new CurrentUserService();

        sut.IsAdmin.Should().BeFalse();
        sut.Role.Should().Be("driver");
    }

    [Fact]
    public void IsAdmin_DebeSerVerdadero_CuandoRoleEsAdmin()
    {
        ICurrentUserService sut = new CurrentUserService { Role = "admin" };

        sut.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void DebePermitirEstablecerLaIdentidadCompleta()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var sut = new CurrentUserService
        {
            UserId = userId,
            TenantId = tenantId,
            Role = "admin",
            Email = "admin@test.com"
        };

        sut.UserId.Should().Be(userId);
        sut.TenantId.Should().Be(tenantId);
        sut.Email.Should().Be("admin@test.com");
    }
}
