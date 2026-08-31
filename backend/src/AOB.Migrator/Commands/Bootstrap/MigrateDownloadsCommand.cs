using System.IO.Compression;
using AOB.Core.Entities;
using AOB.Core.Utilities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AOB.Migrator.Commands.Bootstrap;

public class MigrateDownloadsCommand(
    AppDbContext db,
    MigratorOptions opts,
    SshTunnel tunnel,
    ILogger<MigrateDownloadsCommand> log)
{
    private const int DownloadCategoryLegacyOffset = 100000;

    public async Task RunAsync()
    {
        log.LogInformation("=== Migrate downloads ===");

        var uploadsRoot = Path.Combine(opts.LegacyMediaPath, "uploads-target");
        Directory.CreateDirectory(uploadsRoot);

        var extractedRoots = ExtractDownloadArchives();

        var src = new JoomlaSource(opts, tunnel);
        foreach (var (name, joomla) in src.Sites.Select(s => (s.Name, s.Joomla)))
        {
            log.LogInformation("--- Site: {Name} (SiteId={SiteId}) ---", name, joomla.TargetSiteId);

            using var conn = src.Open(joomla);

            var joomlaCats = await ReadPhocaCategories(conn, joomla.TablePrefix);
            log.LogInformation("Lidas {Count} categorias PhocaDownload", joomlaCats.Count);
            var catLookup = await UpsertDownloadCategories(joomla.TargetSiteId, joomlaCats);

            var files = await ReadPhocaFiles(conn, joomla.TablePrefix);
            log.LogInformation("Lidos {Count} ficheiros PhocaDownload", files.Count);

            var existing = await db.Downloads
                .Where(d => d.SiteId == joomla.TargetSiteId && d.LegacyId != null)
                .ToDictionaryAsync(d => d.LegacyId!.Value);

            var fileIds = files.Select(f => f.Id).ToHashSet();
            var otherSlugs = await db.Downloads
                .Where(d => d.SiteId == joomla.TargetSiteId && (d.LegacyId == null || !fileIds.Contains(d.LegacyId.Value)))
                .Select(d => d.Slug)
                .ToListAsync();
            var slugSeen = new HashSet<string>(otherSlugs, StringComparer.OrdinalIgnoreCase);

            var extractedRoot = extractedRoots.GetValueOrDefault(name);

            int created = 0, updated = 0, skipped = 0, copied = 0;
            foreach (var f in files)
            {
                if (string.IsNullOrWhiteSpace(f.Filename))
                {
                    skipped++;
                    continue;
                }

                var relPath = f.Filename.Replace('\\', '/').TrimStart('/');

                if (!existing.TryGetValue(f.Id, out var dl))
                {
                    dl = new Download
                    {
                        SiteId = joomla.TargetSiteId,
                        LegacyId = f.Id,
                        CreatedAt = f.Date == DateTime.MinValue ? DateTime.UtcNow : f.Date,
                    };
                    db.Downloads.Add(dl);
                    created++;
                }
                else
                {
                    updated++;
                }

                dl.CategoryId = catLookup.GetValueOrDefault(f.CatId);
                dl.Title = f.Title;
                var baseSlug = string.IsNullOrWhiteSpace(f.Alias) ? SlugHelper.Slugify(f.Title, 300) : f.Alias;
                var finalSlug = baseSlug;
                if (!slugSeen.Add(finalSlug))
                {
                    finalSlug = $"{baseSlug}-{f.Id}";
                    slugSeen.Add(finalSlug);
                    log.LogWarning("Slug duplicado '{Base}' — desambiguado para '{Final}' (download id={Id})",
                        baseSlug, finalSlug, f.Id);
                }
                dl.Slug = finalSlug;
                dl.Description = string.IsNullOrWhiteSpace(f.Description) ? null : f.Description;
                dl.FileName = Path.GetFileName(relPath);
                dl.StoragePath = $"/uploads/{name}/downloads/{relPath}";
                dl.FileSize = f.FileSize;
                dl.MimeType = GuessMime(Path.GetExtension(relPath));
                dl.DownloadCount = f.Hits;
                dl.IsPublished = f.Published == 1 && f.Approved == 1;
                dl.PublishedAt = f.PublishUp == DateTime.MinValue ? (f.Date == DateTime.MinValue ? null : f.Date) : f.PublishUp;
                dl.UpdatedAt = DateTime.UtcNow;

                if (extractedRoot is not null)
                {
                    var srcFile = Path.Combine(extractedRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
                    var dstFile = Path.Combine(uploadsRoot, name, "downloads", relPath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(srcFile))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);
                        if (!File.Exists(dstFile) ||
                            new FileInfo(dstFile).Length != new FileInfo(srcFile).Length)
                        {
                            File.Copy(srcFile, dstFile, overwrite: true);
                            copied++;
                        }
                    }
                    else
                    {
                        log.LogWarning("Ficheiro nao encontrado: {File} (download id={Id} '{Title}')",
                            srcFile, f.Id, f.Title);
                    }
                }
            }

            await db.SaveChangesAsync();
            log.LogInformation("Site {Name}: {Created} novos, {Updated} atualizados, {Skipped} skipped, {Copied} ficheiros copiados",
                name, created, updated, skipped, copied);
        }

        log.LogInformation("Uploads copiados para: {Root}", uploadsRoot);
        log.LogInformation("=== Downloads OK ===");
    }

    private Dictionary<string, string> ExtractDownloadArchives()
    {
        var result = new Dictionary<string, string>();
        var tmp = Path.Combine(opts.LegacyMediaPath, "extracted-downloads");
        Directory.CreateDirectory(tmp);

        foreach (var (site, archive) in new[]
        {
            ("aob", Path.Combine(opts.LegacyMediaPath, "aob-media-filtrado.tar.gz")),
            ("bva", Path.Combine(opts.LegacyMediaPath, "bva-media-filtrado.tar.gz")),
        })
        {
            if (!File.Exists(archive))
            {
                log.LogWarning("Arquivo nao encontrado: {Path} (downloads ficam sem ficheiros fisicos)", archive);
                continue;
            }
            var target = Path.Combine(tmp, site);
            var marker = Path.Combine(target, ".extracted");
            if (!File.Exists(marker))
            {
                Directory.CreateDirectory(target);
                log.LogInformation("A extrair {Archive} -> {Target} ...", archive, target);
                ExtractTarGz(archive, target);
                File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            }
            var phocaRoot = FindPhocaRoot(target);
            if (phocaRoot != null) result[site] = phocaRoot;
        }
        return result;
    }

    private static void ExtractTarGz(string archivePath, string destDir)
    {
        using var fs = File.OpenRead(archivePath);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        System.Formats.Tar.TarFile.ExtractToDirectory(gz, destDir, overwriteFiles: true);
    }

    private static string? FindPhocaRoot(string root)
    {
        foreach (var d in Directory.EnumerateDirectories(root, "phocadownload", SearchOption.AllDirectories))
            return d;
        return null;
    }

    private async Task<Dictionary<int, int>> UpsertDownloadCategories(int siteId, List<PhocaCat> cats)
    {
        var existing = await db.Categories
            .Where(c => c.SiteId == siteId && c.LegacyId != null && c.LegacyId >= DownloadCategoryLegacyOffset)
            .ToDictionaryAsync(c => c.LegacyId!.Value - DownloadCategoryLegacyOffset, c => c);

        var phocaIds = cats.Select(c => c.Id).Select(id => (int?)(DownloadCategoryLegacyOffset + id)).ToHashSet();
        var otherSlugs = await db.Categories
            .Where(c => c.SiteId == siteId && (c.LegacyId == null || !phocaIds.Contains(c.LegacyId)))
            .Select(c => c.Slug)
            .ToListAsync();
        var slugSeen = new HashSet<string>(otherSlugs, StringComparer.OrdinalIgnoreCase);

        var byPhocaId = new Dictionary<int, Category>();
        foreach (var pc in cats.OrderBy(c => c.ParentId).ThenBy(c => c.Ordering))
        {
            if (!existing.TryGetValue(pc.Id, out var cat))
            {
                cat = new Category
                {
                    SiteId = siteId,
                    LegacyId = DownloadCategoryLegacyOffset + pc.Id,
                };
                db.Categories.Add(cat);
            }
            cat.Name = pc.Title;
            var baseSlug = "dl-" + (string.IsNullOrWhiteSpace(pc.Alias) ? SlugHelper.Slugify(pc.Title) : pc.Alias);
            var finalSlug = baseSlug;
            if (!slugSeen.Add(finalSlug))
            {
                finalSlug = $"{baseSlug}-{pc.Id}";
                slugSeen.Add(finalSlug);
            }
            cat.Slug = finalSlug;
            cat.Description = string.IsNullOrWhiteSpace(pc.Description) ? null : pc.Description;
            cat.IsPublished = pc.Published == 1;
            cat.SortOrder = pc.Ordering;
            cat.UpdatedAt = DateTime.UtcNow;
            byPhocaId[pc.Id] = cat;
        }

        foreach (var pc in cats)
        {
            if (byPhocaId.TryGetValue(pc.Id, out var cat) &&
                pc.ParentId > 0 && byPhocaId.TryGetValue(pc.ParentId, out var parent))
            {
                cat.Parent = parent;
                cat.ParentId = parent.Id;
            }
        }

        await db.SaveChangesAsync();
        return byPhocaId.ToDictionary(kv => kv.Key, kv => kv.Value.Id);
    }

    private static async Task<List<PhocaCat>> ReadPhocaCategories(MySqlConnection conn, string prefix)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, parent_id, title, alias, description, published, ordering
            FROM {prefix}phocadownload_categories
            ORDER BY parent_id, ordering
        """;
        var list = new List<PhocaCat>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PhocaCat
            {
                Id = r.GetInt32(0),
                ParentId = r.GetInt32(1),
                Title = r.GetString(2),
                Alias = r.GetString(3),
                Description = r.IsDBNull(4) ? "" : r.GetString(4),
                Published = r.GetInt32(5),
                Ordering = r.GetInt32(6),
            });
        }
        return list;
    }

    private static async Task<List<PhocaFile>> ReadPhocaFiles(MySqlConnection conn, string prefix)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, catid, title, alias, filename, filesize, description,
                   date, publish_up, hits, published, approved
            FROM {prefix}phocadownload
            ORDER BY id
        """;
        var list = new List<PhocaFile>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new PhocaFile
            {
                Id = r.GetInt32(0),
                CatId = r.GetInt32(1),
                Title = r.GetString(2),
                Alias = r.GetString(3),
                Filename = r.GetString(4),
                FileSize = r.GetInt32(5),
                Description = r.IsDBNull(6) ? "" : r.GetString(6),
                Date = SafeDate(r, 7),
                PublishUp = SafeDate(r, 8),
                Hits = r.GetInt32(9),
                Published = r.GetInt32(10),
                Approved = r.GetInt32(11),
            });
        }
        return list;
    }

    private static DateTime SafeDate(MySqlDataReader r, int i)
    {
        if (r.IsDBNull(i)) return DateTime.MinValue;
        try { return DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc); }
        catch { return DateTime.MinValue; }
    }

    private static string GuessMime(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".zip" => "application/zip",
        ".rar" => "application/vnd.rar",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".txt" => "text/plain",
        _ => "application/octet-stream",
    };

    private class PhocaCat
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string Title { get; set; } = "";
        public string Alias { get; set; } = "";
        public string Description { get; set; } = "";
        public int Published { get; set; }
        public int Ordering { get; set; }
    }

    private class PhocaFile
    {
        public int Id { get; set; }
        public int CatId { get; set; }
        public string Title { get; set; } = "";
        public string Alias { get; set; } = "";
        public string Filename { get; set; } = "";
        public int FileSize { get; set; }
        public string Description { get; set; } = "";
        public DateTime Date { get; set; }
        public DateTime PublishUp { get; set; }
        public int Hits { get; set; }
        public int Published { get; set; }
        public int Approved { get; set; }
    }
}
