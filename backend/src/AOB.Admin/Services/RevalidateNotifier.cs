using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AOB.Admin.Services;

/// <summary>
/// Notifica o AOB.Api para reencaminhar um pedido de revalidate para o frontend Next.js
/// correspondente ao site. Usado após publicar/editar conteúdo para invalidar o cache ISR.
/// Falhas são silenciosamente ignoradas (best-effort).
/// </summary>
public class RevalidateNotifier(HttpClient http, IConfiguration cfg, ILogger<RevalidateNotifier> log)
{
    public async Task NotifyAsync(string siteSlug, string path = "/", CancellationToken ct = default)
    {
        var apiBase = cfg["Api:BaseUrl"];
        if (string.IsNullOrWhiteSpace(apiBase)) return;

        var secret = cfg["Revalidate:Secret"];
        var url = $"{apiBase.TrimEnd('/')}/api/revalidate?siteSlug={Uri.EscapeDataString(siteSlug)}&path={Uri.EscapeDataString(path)}";
        if (!string.IsNullOrEmpty(secret))
            url += $"&secret={Uri.EscapeDataString(secret)}";

        try
        {
            // Timeout configurado em Program.cs (AddHttpClient); nao pode ser
            // alterado aqui porque este e um typed client partilhado.
            var res = await http.PostAsync(url, content: null, ct);
            if (!res.IsSuccessStatusCode)
                log.LogWarning("Revalidate {Site} {Path}: {Status}", siteSlug, path, res.StatusCode);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Revalidate {Site} {Path} falhou", siteSlug, path);
        }
    }
}
