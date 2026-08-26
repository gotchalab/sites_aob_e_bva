using System.Text;
using System.Text.RegularExpressions;
using AOB.Core.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AOB.Application.Forms;

// Endereco From por site. O dominio e sempre `aobarcelos.pt` (unico
// autenticado no Brevo — SPF/DKIM/DMARC alinhados). A local-part vem do
// slug do site para permitir distinguir a origem no cabecalho From.
// Display name = Site.Name.
public static class SiteMailFromExtensions
{
    // Dominio autenticado no Brevo. Se um dia outro dominio for verificado,
    // usar o `Domain` do Site ou uma configuracao dedicada.
    private const string AuthenticatedDomain = "aobarcelos.pt";

    public static (string FromEmail, string FromName) MailFrom(this Site site)
    {
        var slug = string.IsNullOrWhiteSpace(site.Slug) ? "noreply" : site.Slug.ToLowerInvariant();
        var local = $"noreply-{slug}";
        var email = $"{local}@{AuthenticatedDomain}";
        var name = string.IsNullOrWhiteSpace(site.Name) ? "AOBarcelos" : site.Name;
        return (email, name);
    }
}

/// <summary>
/// SMTP via MailKit (System.Net.Mail.SmtpClient e obsoleto e tem bugs
/// conhecidos com STARTTLS+AUTH em .NET moderno — ver
/// https://learn.microsoft.com/dotnet/api/system.net.mail.smtpclient).
///
/// Modos:
/// - `Smtp:Host` vazio (dev sem MTA) → apenas loga preview e sai.
/// - `Smtp:UseSsl=true` na porta 587 → STARTTLS. Na porta 465 → SMTPS implicito.
/// - Sem User/Password → conexao anonima (Exim4 local, localhost:25).
///
/// Deliverability:
/// - O `From` e SEMPRE de um dominio autenticado no relay (Smtp:From ou
///   fallback `noreply@aobarcelos.pt`). NUNCA usar o email da associacao
///   (@gmail.com) no From — Gmail/DMARC rejeitam como spoofing.
/// - Usar `replyTo` (endereco da associacao) para as respostas irem para
///   a caixa certa sem violar SPF/DKIM/DMARC do dominio do From.
/// </summary>
public class EmailSender(IConfiguration config, ILogger<EmailSender> log)
{
    public record EmailAttachment(string FileName, byte[] Content, string ContentType);

    public Task SendAsync(string to, string subject, string bodyHtml, CancellationToken ct = default)
        => SendAsync(to, subject, bodyHtml, attachments: null, replyTo: null, fromEmail: null, fromName: null, ct);

    public Task SendAsync(
        string to,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments,
        CancellationToken ct = default)
        => SendAsync(to, subject, bodyHtml, attachments, replyTo: null, fromEmail: null, fromName: null, ct);

    public Task SendAsync(
        string to,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments,
        string? replyTo,
        CancellationToken ct = default)
        => SendAsync(to, subject, bodyHtml, attachments, replyTo, fromEmail: null, fromName: null, ct);

    public async Task SendAsync(
        string to,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachment>? attachments,
        string? replyTo,
        string? fromEmail,
        string? fromName,
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
        // From: SEMPRE um endereco de dominio autenticado no relay (Brevo/etc).
        // Ordem de precedencia:
        //   1) fromEmail passado pela chamada (permite dinamica por site)
        //   2) Smtp:From (fallback global do ambiente)
        //   3) noreply@aobarcelos.pt (ultima defesa)
        // O `user` nunca e usado como From (pode ser um gmail do login SMTP,
        // o que ativaria heuristicas FREEMAIL_* em SpamAssassin).
        var effectiveFromEmail = fromEmail;
        if (string.IsNullOrWhiteSpace(effectiveFromEmail))
        {
            effectiveFromEmail = config["Smtp:From"];
        }
        if (string.IsNullOrWhiteSpace(effectiveFromEmail))
        {
            effectiveFromEmail = "noreply@aobarcelos.pt";
            log.LogWarning("Smtp:From nao configurado — usar fallback {From}. Definir Smtp:From no ambiente.", effectiveFromEmail);
        }
        var effectiveFromName = !string.IsNullOrWhiteSpace(fromName)
            ? fromName
            : config["Smtp:FromName"];
        var useSsl = bool.TryParse(config["Smtp:UseSsl"], out var s) && s;

        var msg = new MimeMessage();
        msg.From.Add(string.IsNullOrWhiteSpace(effectiveFromName)
            ? MailboxAddress.Parse(effectiveFromEmail)
            : new MailboxAddress(effectiveFromName, effectiveFromEmail));
        msg.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            try { msg.ReplyTo.Add(MailboxAddress.Parse(replyTo)); }
            catch (ParseException) { log.LogWarning("Reply-To invalido ignorado: {ReplyTo}", replyTo); }
        }
        msg.Subject = subject;

        // Wrap para HTML valido (SpamAssassin penaliza fragmentos soltos)
        // e gerar TextBody a partir do HTML — multipart/alternative reduz
        // significativamente o score em filtros heuristicos.
        var wrappedHtml = WrapHtml(bodyHtml, subject);
        var plainText = HtmlToPlainText(bodyHtml);

        var builder = new BodyBuilder
        {
            HtmlBody = wrappedHtml,
            TextBody = plainText,
        };
        if (attachments is not null)
        {
            foreach (var a in attachments)
                builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        }
        msg.Body = builder.ToMessageBody();

        // List-Unsubscribe (mailto) — Gmail/Yahoo recomendam para emails
        // transaccionais/notificacoes. Aponta para o Reply-To se existir,
        // caso contrario para o proprio From (que a associacao le).
        var unsubscribeAddr = !string.IsNullOrWhiteSpace(replyTo) ? replyTo : effectiveFromEmail;
        msg.Headers.Add("List-Unsubscribe", $"<mailto:{unsubscribeAddr}?subject=unsubscribe>");

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
            log.LogInformation("Email enviado {From} -> {To} (reply-to={ReplyTo}, {Subject}, {N} anexos)",
                effectiveFromEmail, to, replyTo ?? "-", subject, builder.Attachments.Count);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: true, ct);
        }
    }

    // Envolve o corpo num documento HTML minimo mas valido. Se o body ja
    // comeca com <!doctype> ou <html>, devolve tal como esta.
    private static string WrapHtml(string bodyHtml, string subject)
    {
        var trimmed = bodyHtml.TrimStart();
        if (trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
        {
            return bodyHtml;
        }

        var safeTitle = System.Net.WebUtility.HtmlEncode(subject);
        return $"""
        <!doctype html>
        <html lang="pt">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>{safeTitle}</title>
        </head>
        <body style="margin:0;padding:20px;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#222;background:#f8f8f8">
          <div style="max-width:640px;margin:0 auto;background:#fff;padding:24px;border-radius:8px">
        {bodyHtml}
          </div>
        </body>
        </html>
        """;
    }

    // Conversao HTML → texto simples suficiente para o TextBody de
    // multipart/alternative. Nao pretende ser um renderer completo — so
    // preserva paragrafos, quebras e desescapa entidades comuns.
    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var s = html;
        // remover scripts/styles se aparecerem
        s = Regex.Replace(s, "<(script|style)[^>]*>.*?</\\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // quebras de linha em blocos comuns
        s = Regex.Replace(s, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "</(p|div|tr|h[1-6]|li)>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<li[^>]*>", "  - ", RegexOptions.IgnoreCase);
        // remover restantes tags
        s = Regex.Replace(s, "<[^>]+>", string.Empty);
        // desescapar entidades HTML
        s = System.Net.WebUtility.HtmlDecode(s);
        // colapsar espacos em branco preservando quebras de paragrafo
        var sb = new StringBuilder(s.Length);
        var lastWasNewline = false;
        foreach (var line in s.Split('\n'))
        {
            var trimmed = Regex.Replace(line, "[ \\t]+", " ").Trim();
            if (trimmed.Length == 0)
            {
                if (!lastWasNewline)
                {
                    sb.Append('\n');
                    lastWasNewline = true;
                }
                continue;
            }
            sb.Append(trimmed).Append('\n');
            lastWasNewline = false;
        }
        return sb.ToString().Trim();
    }
}
