using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AOB.Application.Forms;

/// <summary>
/// SMTP via MailKit (System.Net.Mail.SmtpClient e obsoleto e tem bugs
/// conhecidos com STARTTLS+AUTH em .NET moderno — ver
/// https://learn.microsoft.com/dotnet/api/system.net.mail.smtpclient).
///
/// Modos:
/// - `Smtp:Host` vazio (dev sem MTA) → apenas loga preview e sai.
/// - `Smtp:UseSsl=true` na porta 587 → STARTTLS. Na porta 465 → SMTPS implicito.
/// - Sem User/Password → conexao anonima (Exim4 local, localhost:25).
/// </summary>
public class EmailSender(IConfiguration config, ILogger<EmailSender> log)
{
    public record EmailAttachment(string FileName, byte[] Content, string ContentType);

    public Task SendAsync(string to, string subject, string bodyHtml, CancellationToken ct = default)
        => SendAsync(to, subject, bodyHtml, attachments: null, fromOverride: null, ct);

    public Task SendAsync(
        string to,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments,
        CancellationToken ct = default)
        => SendAsync(to, subject, bodyHtml, attachments, fromOverride: null, ct);

    public async Task SendAsync(
        string to,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments,
        string? fromOverride,
        CancellationToken ct = default)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            log.LogWarning("Smtp:Host nao configurado — a saltar envio para {To}. Subject={Subject}", to, subject);
            log.LogInformation("Body preview:\n{Body}", bodyHtml.Length > 500 ? bodyHtml[..500] + "..." : bodyHtml);
            if (attachments is not null)
            {
                foreach (var a in attachments)
                    log.LogInformation("Attachment simulado: {Name} ({Size} bytes, {Ct})", a.FileName, a.Content.Length, a.ContentType);
            }
            return;
        }

        // Dev override: redireccionar TODOS os envios para um endereco unico.
        var devRedirect = config["Smtp:DevRedirectTo"];
        if (!string.IsNullOrWhiteSpace(devRedirect))
        {
            log.LogInformation("Smtp:DevRedirectTo activo — a redireccionar {Original} -> {Redirect}", to, devRedirect);
            subject = $"[DEV para {to}] {subject}";
            to = devRedirect;
        }

        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 25;
        var user = config["Smtp:User"];
        var pass = config["Smtp:Password"];
        var fromAddr = fromOverride ?? config["Smtp:From"] ?? user ?? "noreply@aobarcelos.pt";
        var useSsl = bool.TryParse(config["Smtp:UseSsl"], out var s) && s;

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(fromAddr));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = bodyHtml };
        if (attachments is not null)
        {
            foreach (var a in attachments)
                builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        }
        msg.Body = builder.ToMessageBody();

        // Escolha do modo TLS:
        // - port 465 → SslOnConnect (SMTPS)
        // - useSsl && port 587 → StartTls
        // - useSsl && porta arbitraria → StartTlsWhenAvailable
        // - !useSsl → None (Exim4 local sem TLS)
        var secure = useSsl
            ? (port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
            : SecureSocketOptions.None;

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, port, secure, ct);
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass, ct);
            await client.SendAsync(msg, ct);
            log.LogInformation("Email enviado {From} -> {To} ({Subject}, {N} anexos)",
                fromAddr, to, subject, builder.Attachments.Count);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: true, ct);
        }
    }
}
