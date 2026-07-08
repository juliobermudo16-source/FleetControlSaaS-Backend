namespace FleetControl.Infrastructure.Services;

/// <summary>Se enlaza a la seccion "EmailSettings" de appsettings.json. Usar una App Password de Gmail, no la clave normal.</summary>
public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "FleetControl SaaS";
    public string AppPassword { get; set; } = string.Empty; // Gmail App Password (16 caracteres)
}
