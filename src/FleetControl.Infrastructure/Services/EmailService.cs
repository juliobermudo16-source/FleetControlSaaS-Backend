using FleetControl.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FleetControl.Infrastructure.Services;

/// <summary>Envio de correos via Gmail SMTP (MailKit) para las alertas de semaforo.</summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
