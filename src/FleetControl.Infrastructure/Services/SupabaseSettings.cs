namespace FleetControl.Infrastructure.Services;

/// <summary>Se enlaza a la seccion "Supabase" de appsettings.json.</summary>
public class SupabaseSettings
{
    public string Url { get; set; } = string.Empty;          // https://xxxx.supabase.co
    public string ServiceRoleKey { get; set; } = string.Empty; // clave service_role (secreta, solo backend)
    public string JwtSecret { get; set; } = string.Empty;      // Legacy JWT Secret del proyecto Supabase
}
