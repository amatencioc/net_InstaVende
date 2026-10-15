using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InstaVende.Web.Services.Notifications;

/// <summary>Sección "Email": servidor SMTP para notificaciones. Si Host está vacío, el envío se desactiva.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "notificaciones@instavende.app";
    public string FromName { get; set; } = "InstaVende";

    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>Envío de emails transaccionales por SMTP (MailKit).</summary>
public sealed class EmailSender(IOptions<EmailOptions> options, ILogger<EmailSender> logger)
{
    private readonly EmailOptions _opts = options.Value;

    public bool Enabled => _opts.Enabled;

    public async Task SendAsync(IReadOnlyCollection<string> to, string subject, string html, string text, CancellationToken ct)
    {
        if (!_opts.Enabled || to.Count == 0) return;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, _opts.From));
        foreach (var address in to) message.Bcc.Add(MailboxAddress.Parse(address));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_opts.Host!, _opts.Port, _opts.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrEmpty(_opts.Username))
            await client.AuthenticateAsync(_opts.Username, _opts.Password ?? string.Empty, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
        logger.LogInformation("Email \"{Subject}\" enviado a {Count} destinatarios", subject, to.Count);
    }
}
