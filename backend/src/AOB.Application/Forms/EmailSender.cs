using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AOB.Application.Forms;

/// <summary>
/// SMTP simples. Replica o comportamento do Joomla no VPS antigo:
/// - Producao Debian: aponta para "localhost:25" (Exim4 nativo, sem auth) e envia
///   com o "From" do site (equivalente ao mailfrom do Joomla).
/// - Dev Windows sem MTA local: `Smtp:Host` vazio → apenas loga o preview.
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

        // Dev override: redireccionar TODOS os envios para um endereco unico
        // (evita mandar emails reais durante testes). Configurar via
        // Smtp:DevRedirectTo em appsettings.Development.json.
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
        var from = fromOverride ?? config["Smtp:From"] ?? user ?? "noreply@aobarcelos.pt";
        // Default sem SSL para replicar o Joomla + Exim4 local (localhost:25). Se
        // aparecer explicitamente `Smtp:UseSsl=true`, activa STARTTLS.
        var useSsl = bool.TryParse(config["Smtp:UseSsl"], out var s) && s;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            Credentials = string.IsNullOrEmpty(user) ? null : new NetworkCredential(user, pass),
        };
        using var msg = new MailMessage(from, to, subject, bodyHtml) { IsBodyHtml = true };
        var disposables = new List<IDisposable>();
        if (attachments is not null)
        {
            foreach (var a in attachments)
            {
                var stream = new MemoryStream(a.Content, writable: false);
                disposables.Add(stream);
                var att = new Attachment(stream, a.FileName, a.ContentType);
                disposables.Add(att);
                msg.Attachments.Add(att);
            }
        }
        try
        {
            await client.SendMailAsync(msg, ct);
            log.LogInformation("Email enviado {From} -> {To} ({Subject}, {N} anexos)", from, to, subject, msg.Attachments.Count);
        }
        finally
        {
            foreach (var d in disposables) d.Dispose();
        }
    }
}
