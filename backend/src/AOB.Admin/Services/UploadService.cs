using System.Text.RegularExpressions;

namespace AOB.Admin.Services;

/// <summary>
/// Guarda ficheiros em uploads/{siteSlug}/{kind}/{yyyy}/{mm}/{safeName}
/// Aplica whitelist de extensoes e devolve o caminho publico + tamanho + mime.
/// </summary>
public class UploadService(IConfiguration config, ILogger<UploadService> log, ClamAvScanner scanner)
{
    private static readonly HashSet<string> DownloadExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".zip", ".rar", ".7z", ".txt", ".csv", ".odt", ".ods",
    };

    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg",
    };

    // Kinds que aceitam imagens (whitelist para path segmentation em uploads/{site}/{kind}/...)
    private static readonly HashSet<string> ImageKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "images", "sponsors", "home",
    };

    private static readonly Regex SafeCharsRx = new(@"[^a-zA-Z0-9._-]", RegexOptions.Compiled);

    private string RootPath => config["Uploads:RootPath"]
        ?? throw new InvalidOperationException("Uploads:RootPath nao configurado");

    public record UploadResult(string PublicPath, string FileName, long FileSize, string MimeType);

    public async Task<UploadResult> SaveAsync(Stream stream, string originalName, string siteSlug, string kind, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalName);
        var allowed = ImageKinds.Contains(kind) ? ImageExts : DownloadExts;
        if (!allowed.Contains(ext))
            throw new InvalidOperationException($"Extensao '{ext}' nao permitida para '{kind}'");

        // Buffer para permitir scan + gravacao numa unica passagem
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        try
        {
            await scanner.ScanAsync(buffer, ct);
        }
        catch (ClamAvScanner.MalwareDetected ex)
        {
            log.LogWarning("Upload rejeitado ({Name}): {Signature}", originalName, ex.Message);
            throw;
        }

        buffer.Position = 0;

        var safeName = SafeCharsRx.Replace(Path.GetFileNameWithoutExtension(originalName), "-").Trim('-');
        if (string.IsNullOrEmpty(safeName)) safeName = "file";
        var now = DateTime.UtcNow;
        var final = $"{safeName}-{now:HHmmss}{ext.ToLowerInvariant()}";
        var relDir = Path.Combine(siteSlug, kind, now.ToString("yyyy"), now.ToString("MM"));
        var absDir = Path.Combine(RootPath, relDir);
        Directory.CreateDirectory(absDir);
        var absPath = Path.Combine(absDir, final);

        await using (var fs = File.Create(absPath))
            await buffer.CopyToAsync(fs, ct);

        var size = new FileInfo(absPath).Length;
        log.LogInformation("Upload {Site}/{Kind}: {File} ({Size} bytes)", siteSlug, kind, final, size);

        var publicPath = "/uploads/" + Path.Combine(relDir, final).Replace('\\', '/');
        return new UploadResult(publicPath, final, size, GuessMime(ext));
    }

    /// <summary>
    /// Apaga um ficheiro previamente guardado, dado o seu <c>PublicPath</c>
    /// (formato <c>/uploads/…</c>). Silencioso se o path for invalido ou o
    /// ficheiro ja nao existir. Rejeita paths que escapem de <see cref="RootPath"/>.
    /// </summary>
    public void DeleteStored(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath)) return;
        const string prefix = "/uploads/";
        if (!publicPath.StartsWith(prefix, StringComparison.Ordinal)) return;

        var rel = publicPath.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(RootPath);
        var abs = Path.GetFullPath(Path.Combine(root, rel));
        if (!abs.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            if (File.Exists(abs)) File.Delete(abs);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Falha ao apagar ficheiro {Path}", abs);
        }
    }

    private static string GuessMime(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".zip" => "application/zip",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream",
    };
}
