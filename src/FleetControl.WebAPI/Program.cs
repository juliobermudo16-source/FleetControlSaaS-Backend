using FleetControl.Application;
using FleetControl.Infrastructure;
using FleetControl.WebAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

// --- Servicios ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FleetControl SaaS API",
        Version = "v1",
        Description = "API multi-tenant de control de flota de vehiculos."
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Token JWT de Supabase Auth. Ejemplo: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplicationServices();

// Indicar el entorno de hosting a Infrastructure para que pueda usar InMemory
// cuando los tests ejecutan con WebApplicationFactory (setea EnvironmentName="Testing").
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

// --- CORS: permite al frontend (Vite local y Vercel) llamar a la API ---
const string CorsPolicy = "FleetControlCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://fleet-control-frontend.vercel.app")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// --- Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseFleetControlExceptionHandling();
app.UseCors(CorsPolicy);
app.UseHttpsRedirection();

// Middleware personalizado: valida el JWT de Supabase y resuelve TenantId/Role
app.UseSupabaseJwtAuthentication();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "FleetControl.WebAPI" }));

app.Run();

// Necesario para que WebApplicationFactory<Program> funcione en los tests de integracion.
public partial class Program { }