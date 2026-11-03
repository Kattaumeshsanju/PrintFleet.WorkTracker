using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
    Task SendRichAsync(string to, string subject, string text, string html, string? replyTo, string? replyName, CancellationToken ct);
}

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options) => _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress.Trim()));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        await TransmitAsync(message, ct);
    }

    public async Task SendRichAsync(string to, string subject, string text, string html, string? replyTo, string? replyName, CancellationToken ct)
    {
        var message = new MimeMessage();
        var display = string.IsNullOrWhiteSpace(replyName) ? _options.FromName : $"{replyName.Trim()} via WorkTracker";
        message.From.Add(new MailboxAddress(display, _options.FromAddress.Trim()));
        if (!string.IsNullOrWhiteSpace(replyTo))
            message.ReplyTo.Add(new MailboxAddress(replyName?.Trim() ?? replyTo, replyTo.Trim()));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        var alternative = new MultipartAlternative
        {
            new TextPart("plain") { Text = text },
            new TextPart("html") { Text = html }
        };
        message.Body = alternative;
        await TransmitAsync(message, ct);
    }

    private async Task TransmitAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host.Trim(), _options.Port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_options.Username.Trim(), _options.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
