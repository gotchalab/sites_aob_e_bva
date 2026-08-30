using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands.Bootstrap;

/// <summary>
/// Copia as pastas /images/ de cada tar.gz para uploads-target/{site}/images/.
/// Aplica whitelist de extensoes seguras (sem .php, .html, .htaccess, etc).
/// </summary>
public class MigrateImagesCommand(MigratorOptions opts, ILogger<MigrateImagesCommand> log)
{
    private static readonly HashSet<string> SafeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg",
        ".pdf", ".mp3", ".mp4", ".webm", ".ogg",
        ".ico", ".bmp",
    };

    public async Task RunAsync()
    {
        log.LogInformation("=== Migrate images ===");
        var uploadsRoot = Path.Combine(opts.LegacyMediaPath, "uploads-target");
        var tmp = Path.Combine(opts.LegacyMediaPath, "extracted-downloads");
        Directory.CreateDirectory(uploadsRoot);
        Directory.CreateDirectory(tmp);

        foreach (var (site, archive) in new[]
        {
            ("aob", Path.Combine(opts.LegacyMediaPath, "aob-media-filtrado.tar.gz")),
            ("bva", Path.Combine(opts.LegacyMediaPath, "bva-media-filtrado.tar.gz")),
        })
        {
            if (!File.Exists(archive))
            {
                log.LogWarning("Arquivo nao encontrado: {Path}", archive);
                continue;
            }
            var extractRoot = Path.Combine(tmp, site);
            var marker = Path.Combine(extractRoot, ".extracted");
            if (!File.Exists(marker))
            {
                Directory.CreateDirectory(extractRoot);
                log.LogInformation("A extrair {Archive} ...", archive);
                ExtractTarGz(archive, extractRoot);
                await File.WriteAllTextAsync(marker, DateTime.UtcNow.ToString("O"));
            }

            var imagesRoot = FindImagesRoot(extractRoot);
            if (imagesRoot is null)
            {
                log.LogWarning("Nao encontrei pasta 'images' em {Root}", extractRoot);
                continue;
            }

            var target = Path.Combine(uploadsRoot, site, "images");
            Directory.CreateDirectory(target);

            int copied = 0, skipped = 0;
            foreach (var file in Directory.EnumerateFiles(imagesRoot, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (!SafeExtensions.Contains(ext))
                {
                    skipped++;
                    continue;
                }
                var rel = Path.GetRelativePath(imagesRoot, file);
                var dst = Path.Combine(target, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(file).Length)
                {
                    File.Copy(file, dst, overwrite: true);
                    copied++;
                }
            }
            log.LogInformation("Site {Site}: {Copied} imagens copiadas, {Skipped} skipped por extensao",
                site, copied, skipped);
        }

        log.LogInformation("Uploads em: {Root}", uploadsRoot);
        log.LogInformation("=== Images OK ===");
    }

    private static void ExtractTarGz(string archivePath, string destDir)
    {
        using var fs = File.OpenRead(archivePath);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        System.Formats.Tar.TarFile.ExtractToDirectory(gz, destDir, overwriteFiles: true);
    }

    private static string? FindImagesRoot(string root)
    {
        foreach (var d in Directory.EnumerateDirectories(root, "images", SearchOption.AllDirectories))
            return d;
        return null;
    }
}
