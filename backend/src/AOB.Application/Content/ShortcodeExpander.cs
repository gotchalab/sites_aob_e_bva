using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AOB.Application.Content;

/// <summary>
/// Expande shortcodes Joomla (Phoca Download, Loadposition, Gallery, GoogleMaps)
/// para HTML real. Chamado no server ao devolver o conteudo do artigo.
/// </summary>
public class ShortcodeExpander(AppDbContext db, IConfiguration config)
{
    private static readonly Regex PhocaFileRx = new(
        @"\{phocadownload\s+view=file\|id=(?<id>\d+)(?:\|[^}]*)?\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhocaCategoryRx = new(
        @"\{phocadownload\s+view=category\|id=(?<id>\d+)(?:\|[^}]*)?\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Shortcode novo (downloads criados no admin, sem LegacyId).
    // Aceita: {download id=42} ou {download 42}
    private static readonly Regex DownloadRx = new(
        @"\{download(?:\s+id)?=?\s*(?<id>\d+)\s*\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LoadPositionRx = new(
        @"\{loadposition\s+[^}]+\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GalleryRx = new(
        @"\{gallery\}(?<body>[^{]+)\{/gallery\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GoogleMapsRx = new(
        @"\{googleMaps\s+(?<attrs>[^}]+)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Plugin MosMap (Joomla) usado em posts antigos. Formato:
    // {mosmap width='100%'|height='400'|lat='41.5'|lon='-8.6'|text='Sede'|...}
    private static readonly Regex MosMapRx = new(
        @"\{mosmap\s+(?<attrs>[^}]+)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Links inseridos via CKEditor que apontam para a página de detalhe em vez do ficheiro.
    // Ex: href="/downloads/lista-classes-bva-2025"
    private static readonly Regex DownloadPageLinkRx = new(
        @"href=""(?:/downloads/(?<slug>[^""?#]+))""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Remove tags HTML dentro dos valores dos atributos do mosmap
    // (posts antigos têm <span style="...">41.5</span> em vez de 41.5)
    private static readonly Regex StripTagsRx = new(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif",
    };

    public async Task<string> ExpandAsync(string html, int siteId, string siteSlug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        html = await RewriteDownloadPageLinksAsync(html, siteId, ct);

        if (!html.Contains('{')) return html;

        html = await ExpandPhocaAsync(html, siteId, ct);
        html = await ExpandDownloadsAsync(html, siteId, ct);
        html = ExpandGallery(html, siteSlug);
        html = ExpandGoogleMaps(html);
        html = ExpandMosMap(html);
        html = LoadPositionRx.Replace(html, "");
        return html;
    }

    // Reescreve <a href="/downloads/slug"> para apontar diretamente para o ficheiro (/uploads/...).
    // Assim o clique abre o PDF no browser ou descarrega os outros tipos sem passar pela página de detalhe.
    private async Task<string> RewriteDownloadPageLinksAsync(string html, int siteId, CancellationToken ct)
    {
        if (!html.Contains("/downloads/", StringComparison.OrdinalIgnoreCase)) return html;

        var slugs = DownloadPageLinkRx.Matches(html)
            .Select(m => m.Groups["slug"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (slugs.Count == 0) return html;

        var paths = await db.Downloads.AsNoTracking()
            .Where(d => d.SiteId == siteId && slugs.Contains(d.Slug) && d.StoragePath != "")
            .ToDictionaryAsync(d => d.Slug, d => d.StoragePath, StringComparer.OrdinalIgnoreCase, ct);

        return DownloadPageLinkRx.Replace(html, m =>
        {
            var slug = m.Groups["slug"].Value;
            return paths.TryGetValue(slug, out var path)
                ? $"href=\"{Escape(path)}\""
                : m.Value;
        });
    }

    private async Task<string> ExpandDownloadsAsync(string html, int siteId, CancellationToken ct)
    {
        var ids = DownloadRx.Matches(html).Select(m => int.Parse(m.Groups["id"].Value)).ToHashSet();
        if (ids.Count == 0) return html;

        var files = await db.Downloads.AsNoTracking()
            .Where(d => d.SiteId == siteId && ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        return DownloadRx.Replace(html, m =>
        {
            var id = int.Parse(m.Groups["id"].Value);
            if (files.TryGetValue(id, out var f))
                return RenderFileLink(f);
            return $"<em class=\"text-muted\">[Download id={id} indisponivel]</em>";
        });
    }

    private async Task<string> ExpandPhocaAsync(string html, int siteId, CancellationToken ct)
    {
        var fileIds = PhocaFileRx.Matches(html).Select(m => int.Parse(m.Groups["id"].Value)).ToHashSet();
        var files = fileIds.Count > 0
            ? await db.Downloads.AsNoTracking()
                .Where(d => d.SiteId == siteId && d.LegacyId != null && fileIds.Contains(d.LegacyId.Value))
                .ToDictionaryAsync(d => d.LegacyId!.Value, ct)
            : new Dictionary<int, Download>();

        html = PhocaFileRx.Replace(html, m =>
        {
            var id = int.Parse(m.Groups["id"].Value);
            if (files.TryGetValue(id, out var f))
                return RenderFileLink(f);
            return $"<em class=\"text-muted\">[Ficheiro id={id} indisponivel]</em>";
        });

        html = PhocaCategoryRx.Replace(html, m =>
        {
            var id = int.Parse(m.Groups["id"].Value);
            return $"<a href=\"/downloads?category=cat-{id}\">Ver downloads desta categoria</a>";
        });

        return html;
    }

    private string ExpandGallery(string html, string siteSlug)
    {
        return GalleryRx.Replace(html, m =>
        {
            var body = m.Groups["body"].Value.Trim();
            var parts = body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";

            var folder = SanitizeFolderName(parts[0]);
            if (string.IsNullOrEmpty(folder)) return "";

            var rootPath = config["Uploads:RootPath"];
            if (string.IsNullOrWhiteSpace(rootPath)) return "";

            var fullPath = Path.Combine(rootPath, siteSlug, "images", folder);
            if (!Directory.Exists(fullPath))
                return $"<div class=\"gallery-empty text-muted small\">Galeria &quot;{Escape(folder)}&quot; sem imagens.</div>";

            var images = Directory.EnumerateFiles(fullPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (images.Count == 0)
                return $"<div class=\"gallery-empty text-muted small\">Galeria &quot;{Escape(folder)}&quot; sem imagens.</div>";

            var sb = new StringBuilder();
            sb.Append("<div class=\"gallery grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 my-4\">");
            foreach (var name in images)
            {
                var url = $"/uploads/{Uri.EscapeDataString(siteSlug)}/images/{Uri.EscapeDataString(folder)}/{Uri.EscapeDataString(name!)}";
                sb.Append($"<a href=\"{url}\" target=\"_blank\" rel=\"noopener\" class=\"gallery-item block overflow-hidden rounded-lg border border-slate-200 bg-slate-100 aspect-square\">");
                sb.Append($"<img src=\"{url}\" alt=\"\" loading=\"lazy\" class=\"h-full w-full object-cover transition hover:scale-105\" />");
                sb.Append("</a>");
            }
            sb.Append("</div>");
            return sb.ToString();
        });
    }

    private static string ExpandGoogleMaps(string html)
    {
        return GoogleMapsRx.Replace(html, m =>
        {
            var attrs = ParseAttrs(m.Groups["attrs"].Value);
            attrs.TryGetValue("label", out var label);
            return RenderMap(attrs, "lat", "long", label);
        });
    }

    private static string ExpandMosMap(string html)
    {
        return MosMapRx.Replace(html, m =>
        {
            var attrs = ParseAttrs(m.Groups["attrs"].Value);
            attrs.TryGetValue("text", out var text);
            return RenderMap(attrs, "lat", "lon", text);
        });
    }

    private static string RenderMap(Dictionary<string, string> attrs, string latKey, string lngKey, string? label)
    {
        if (!attrs.TryGetValue(latKey, out var latStr) || !attrs.TryGetValue(lngKey, out var lngStr))
            return "";

        latStr = StripTagsRx.Replace(latStr, "").Trim();
        lngStr = StripTagsRx.Replace(lngStr, "").Trim();
        if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return "";
        if (!double.TryParse(lngStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)) return "";

        var latS = lat.ToString("0.######", CultureInfo.InvariantCulture);
        var lngS = lng.ToString("0.######", CultureInfo.InvariantCulture);
        var src = $"https://www.google.com/maps?q={latS},{lngS}&z=15&output=embed";
        var cleanLabel = string.IsNullOrWhiteSpace(label) ? "" : StripTagsRx.Replace(label, "").Trim();
        var caption = string.IsNullOrWhiteSpace(cleanLabel) ? "" :
            $"<div class=\"text-sm text-slate-600 mt-1\">📍 {Escape(cleanLabel)}</div>";
        return $"""
            <div class="google-maps my-4">
              <div class="overflow-hidden rounded-lg border border-slate-200">
                <iframe src="{src}" width="100%" height="360" loading="lazy" referrerpolicy="no-referrer-when-downgrade" style="border:0;display:block"></iframe>
              </div>
              {caption}
            </div>
            """;
    }

    private static Dictionary<string, string> ParseAttrs(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rx = new Regex(@"(?<k>\w+)\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>\S+))");
        foreach (Match m in rx.Matches(raw))
            dict[m.Groups["k"].Value] = m.Groups["v"].Value;
        return dict;
    }

    private static string SanitizeFolderName(string name)
    {
        var trimmed = name.Trim().TrimStart('/', '\\');
        if (trimmed.Contains("..") || trimmed.Contains('/') || trimmed.Contains('\\')) return "";
        return trimmed;
    }

    private static string RenderFileLink(Download f)
    {
        var size = f.FileSize > 0 ? FormatBytes(f.FileSize) : "";
        return $"""
            <div class="download-shortcode my-3 rounded border border-slate-200 bg-slate-50 p-3">
              <a href="{Escape(f.StoragePath)}" target="_blank" rel="noopener" class="fw-semibold text-decoration-none">
                📄 {Escape(f.Title)}
              </a>
              {(string.IsNullOrEmpty(size) ? "" : $"<span class=\"text-muted small ms-2\">({size})</span>")}
            </div>
            """;
    }

    private static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string FormatBytes(long b)
    {
        if (b < 1024) return $"{b} B";
        var kb = b / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        return $"{kb / 1024:0.#} MB";
    }
}
