using FleetControl.Domain.Entities;
using FleetControl.Domain.Enums;
using FleetControl.Infrastructure.Persistence;
using FleetControl.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Linq;

namespace FleetControl.WebAPI.IntegrationTests;

/// <summary>
/// Host de pruebas que reemplaza Postgres por EF Core InMemory y fuerza el
/// JwtSecret de Supabase a un valor conocido, para poder firmar tokens de
/// prueba con TestJwtGenerator. Expone dos tenants ya sembrados para las
/// pruebas de aislamiento multi-tenant.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Guid TenantAId { get; } = Guid.NewGuid();
    public Guid TenantBId { get; } = Guid.NewGuid();
    public Guid AdminTenantAId { get; } = Guid.NewGuid();
    public Guid DriverTenantAId { get; } = Guid.NewGuid();
    public Guid AdminTenantBId { get; } = Guid.NewGuid();
    public Guid VehicleTenantAId { get; } = Guid.NewGuid();
    public Guid VehicleTenantBId { get; } = Guid.NewGuid();
    public Guid InactiveUserTenantAId { get; } = Guid.NewGuid();
    public Guid MaintenanceTypeId { get; } = Guid.NewGuid();

    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _seeded = false;

    public void EnsureSeeded()
    {
        if (_seeded) return;
        // Services becomes available after the host is created by CreateClient/EnsureServer
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        SeedData(db);
        _seeded = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // 1. Remover cualquier registro previo del DbContext de ApplicationDbContext.
            var descriptors = services.Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) || d.ServiceType == typeof(ApplicationDbContext)).ToList();
            foreach (var d in descriptors) services.Remove(d);

            // 2. Registrar InMemory para las pruebas.
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_dbName));

            // 3. Forzar el JwtSecret de prueba (para que coincida con TestJwtGenerator).
            services.PostConfigure<SupabaseSettings>(opts => opts.JwtSecret = TestJwtGenerator.TestSecret);

            // 3.b Reemplazar el servicio de almacenamiento por un fake en memoria
            // para evitar llamadas HTTP reales a Supabase durante las pruebas.
            var storageDescriptors = services.Where(d => d.ServiceType == typeof(FleetControl.Application.Common.Interfaces.ISupabaseStorageService) || d.ImplementationType == typeof(FleetControl.Infrastructure.Services.SupabaseStorageService)).ToList();
            foreach (var d in storageDescriptors) services.Remove(d);
            services.AddSingleton<FakeSupabaseStorageService>();
            services.AddSingleton<FleetControl.Application.Common.Interfaces.ISupabaseStorageService>(p => p.GetRequiredService<FakeSupabaseStorageService>());

            // 4. Sembrar datos ya que el proveedor InMemory está registrado ahora.
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            SeedData(db);
            _seeded = true;
        });
    }


    private void SeedData(ApplicationDbContext db)
    {
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, CompanyName = "Transportes A" },
            new Tenant { Id = TenantBId, CompanyName = "Transportes B" });

        db.Users.AddRange(
            new AppUser { Id = AdminTenantAId, TenantId = TenantAId, FullName = "Admin A", Email = "admin.a@test.com", Role = UserRole.Admin },
            new AppUser { Id = DriverTenantAId, TenantId = TenantAId, FullName = "Conductor A", Email = "driver.a@test.com", Role = UserRole.Driver },
            new AppUser { Id = InactiveUserTenantAId, TenantId = TenantAId, FullName = "Inactivo A", Email = "inactive.a@test.com", Role = UserRole.Driver, IsActive = false },
            new AppUser { Id = AdminTenantBId, TenantId = TenantBId, FullName = "Admin B", Email = "admin.b@test.com", Role = UserRole.Admin });

        db.Vehicles.AddRange(
            new Vehicle { Id = VehicleTenantAId, TenantId = TenantAId, LicensePlate = "AAA-111", Brand = "Toyota", Model = "Hilux", ManufactureYear = 2022, CurrentMileage = 10000 },
            new Vehicle { Id = VehicleTenantBId, TenantId = TenantBId, LicensePlate = "BBB-222", Brand = "Kia", Model = "Rio", ManufactureYear = 2021, CurrentMileage = 5000 });

        db.MaintenanceTypes.Add(new MaintenanceType { Id = MaintenanceTypeId, TenantId = null, Code = MaintenanceTypeCode.OilChange, Name = "Cambio de aceite", IntervalKm = 10000, EstimatedCost = 100 });

        db.SaveChanges();
    }
}
