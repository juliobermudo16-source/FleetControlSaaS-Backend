using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetControl.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FleetControl.Infrastructure.Services;

/// <summary>
/// Envio de correo via la API HTTPS de Resend (no SMTP: Railway bloquea los
/// puertos SMTP salientes en los planes no-Pro, asi que cualquier envio via
/// MailKit/SmtpClient se queda colgado con TimeoutException).
/// </summary>
public class EmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly EmailSettings _settings;

    public EmailService(HttpClient http, IOptions<EmailSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
        _http.BaseAddress = new Uri("https://api.resend.com/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var payload = new
        {
            from = $"{_settings.SenderName} <{_settings.SenderEmail}>",
            to = new[] { toEmail },
            subject,
            html = htmlBody
        };

        var response = await _http.PostAsJsonAsync("emails", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Resend respondio {(int)response.StatusCode}: {error}");
        }
    }
}
