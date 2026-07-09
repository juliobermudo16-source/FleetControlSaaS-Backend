namespace FleetControl.Infrastructure.Services;

/// <summary>
/// Se enlaza a la seccion "EmailSettings" de appsettings.json. Usa la API HTTPS
/// de Resend (no SMTP): Railway bloquea los puertos SMTP salientes (25/465/587)
/// en los planes no-Pro, asi que el envio de correo debe ir por HTTPS.
/// </summary>
public class EmailSettings
{
    public string ApiKey { get; set; } = string.Empty; // Resend API key (re_...)
    public string SenderEmail { get; set; } = "onboarding@resend.dev";
    public string SenderName { get; set; } = "FleetControl SaaS";
}
