namespace FleetControl.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
