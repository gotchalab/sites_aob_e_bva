using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands.Bootstrap;

/// <summary>
/// Faz download de imagens externas (URLs http/https em artigos vindos do Joomla) para
/// {LegacyMediaPath}/uploads-target/{siteSlug}/images/imported/{sha}.{ext}
/// Devolve o path web (/uploads/...) ou null em caso de erro.
/// </summary>
public sealed class ExternalImageDownloader : IDisposable
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/svg+xml",
    };

    private readonly HttpClient _http;
    private readonly MigratorOptions _opts;
    private readonly ILogger<ExternalImageDownloader> _log;

    public ExternalImageDownloader(MigratorOptions opts, ILogger<ExternalImageDownloader> log)
    {
        _opts = opts;
        _log = log;
        _http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(25),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; AOBMigrator/1.0; +https://aobarcelos.pt)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("image/*,*/*;q=0.8");
    }

    /// <summary>Devolve o path web local (/uploads/...) ou null se falhar.</summary>
    public async Task<string?> DownloadAsync(string url, string siteSlug, CancellationToken ct = default)
        => (await DownloadWithSizeAsync(url, siteSlug, ct)).Path;

    /// <summary>Como DownloadAsync mas devolve também o tamanho em bytes.</summary>
    public async Task<(string? Path, long Bytes)> DownloadWithSizeAsync(string url, string siteSlug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return (null, 0);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return (null, 0);

        var uploadsRoot = Path.Combine(_opts.LegacyMediaPath, "uploads-target");
        var importedDir = Path.Combine(uploadsRoot, siteSlug, "images", "imported");
        Directory.CreateDirectory(importedDir);

        var urlHash = Sha256Hex(url)[..16];

        // Cache: se já existe um ficheiro com este hash, reutiliza (tamanho lido do disco).
        var existing = Directory.GetFiles(importedDir, urlHash + ".*").FirstOrDefault();
        if (existing != null)
        {
            var size = new FileInfo(existing).Length;
            return ($"/uploads/{siteSlug}/images/imported/{Path.GetFileName(existing)}", size);
        }

        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Download falhou ({Status}): {Url}", (int)resp.StatusCode, url);
                return (null, 0);
            }

            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!string.IsNullOrEmpty(contentType) && !AllowedContentTypes.Contains(contentType))
            {
                _log.LogWarning("Content-Type nao permitido '{Ct}': {Url}", contentType, url);
                return (null, 0);
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length < 100)
            {
                _log.LogWarning("Payload muito pequeno ({Bytes}b): {Url}", bytes.Length, url);
                return (null, 0);
            }

            var ext = ResolveExtension(contentType, bytes, uri);
            var fileName = urlHash + ext;
            var absPath = Path.Combine(importedDir, fileName);
            await File.WriteAllBytesAsync(absPath, bytes, ct);
            _log.LogInformation("Imagem descarregada: {Url} -> {File} ({Bytes}b)", url, fileName, bytes.Length);
            return ($"/uploads/{siteSlug}/images/imported/{fileName}", bytes.Length);
        }
        catch (Exception ex)
        {
            _log.LogWarning("Excecao no download {Url}: {Msg}", url, ex.Message);
            return (null, 0);
        }
    }

    private static string ResolveExtension(string contentType, byte[] bytes, Uri uri)
    {
        // 1) magic bytes
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return ".png";
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return ".gif";
            if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return ".webp";
        }
        // 2) Content-Type
        var byCt = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => null,
        };
        if (byCt != null) return byCt;
        // 3) URL extension
        var pathExt = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        return pathExt is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" ? pathExt : ".bin";
    }

    private static string Sha256Hex(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose() => _http.Dispose();
}
