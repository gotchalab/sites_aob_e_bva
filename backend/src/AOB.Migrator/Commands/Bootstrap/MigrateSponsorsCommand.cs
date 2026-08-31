using System.Text.Json;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AOB.Migrator.Commands.Bootstrap;

/// <summary>
/// Migra os banners da tabela Joomla <c>{prefix}banners</c> para a nova entidade Sponsor.
/// Só publica banners com <c>state=1</c> e categoria 15 (BVA). O AOB não tinha banners
/// publicados no Joomla — a tabela u8zjq_banners está vazia — pelo que este comando
/// não cria nada para o site AOB (fica ao critério do admin adicionar via backoffice).
///
/// Copia também os ficheiros dos logos para uploads-target/{site}/sponsors/{slug}{ext}.
/// </summary>
public class MigrateSponsorsCommand(
    AppDbContext db,
    MigratorOptions opts,
    SshTunnel tunnel,
    IConfiguration cfg,
    ILogger<MigrateSponsorsCommand> log)
{
    public async Task RunAsync()
    {
        log.LogInformation("=== Migrate sponsors ===");

        var uploadsRoot = cfg["Uploads:RootPath"] ?? Path.Combine(opts.LegacyMediaPath, "uploads-target");
        var src = new JoomlaSource(opts, tunnel);

        foreach (var (name, joomla) in src.Sites.Select(s => (s.Name, s.Joomla)))
        {
            log.LogInformation("--- Site: {Name} (SiteId={SiteId}) ---", name, joomla.TargetSiteId);

            List<JoomlaBanner> banners;
            using (var conn = src.Open(joomla))
            {
                banners = await ReadJoomlaBanners(conn, joomla.TablePrefix);
            }
            log.LogInformation("Lidos {Count} banners publicados do Joomla", banners.Count);

            if (banners.Count == 0)
            {
                log.LogInformation("Sem banners publicados — nada a migrar para {Site}", name);
                continue;
            }

            var existing = await db.Sponsors
                .Where(s => s.SiteId == joomla.TargetSiteId && s.LegacyId != null)
                .ToDictionaryAsync(s => s.LegacyId!.Value);

            var legacyImagesRoot = Path.Combine(opts.LegacyMediaPath, "uploads-target", name, "images");
            var sponsorsTargetDir = Path.Combine(uploadsRoot, name, "sponsors");
            Directory.CreateDirectory(sponsorsTargetDir);

            int created = 0, updated = 0, imgOk = 0, imgMissing = 0;

            foreach (var b in banners)
            {
                var imageUrl = ExtractImageUrl(b.Params);
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    log.LogWarning("Banner id={Id} '{Name}' sem imagem — SKIP", b.Id, b.Name);
                    continue;
                }

                // imageUrl é tipo "images/banners/final/CM_barcelos.jpg" — retiramos o prefixo "images/"
                var relFromImages = imageUrl.StartsWith("images/", StringComparison.OrdinalIgnoreCase)
                    ? imageUrl[7..]
                    : imageUrl;
                var sourceFile = Path.Combine(legacyImagesRoot, relFromImages.Replace('/', Path.DirectorySeparatorChar));
                var ext = Path.GetExtension(sourceFile);
                var slug = string.IsNullOrWhiteSpace(b.Alias) ? Slugify(b.Name) : b.Alias;
                var destFile = Path.Combine(sponsorsTargetDir, $"{slug}{ext}");
                var publicPath = $"/uploads/{name}/sponsors/{slug}{ext}";

                if (File.Exists(sourceFile))
                {
                    if (!File.Exists(destFile) || new FileInfo(destFile).Length != new FileInfo(sourceFile).Length)
                        File.Copy(sourceFile, destFile, overwrite: true);
                    imgOk++;
                }
                else
                {
                    log.LogWarning("Ficheiro do logo em falta: {Source} — banner id={Id} '{Name}'",
                        sourceFile, b.Id, b.Name);
                    imgMissing++;
                    continue;
                }

                if (!existing.TryGetValue(b.Id, out var sp))
                {
                    sp = new Sponsor
                    {
                        SiteId = joomla.TargetSiteId,
                        LegacyId = b.Id,
                        CreatedAt = DateTime.UtcNow,
                    };
                    db.Sponsors.Add(sp);
                    created++;
                }
                else
                {
                    updated++;
                }

                sp.Name = b.Name;
                sp.Slug = slug;
                sp.LogoPath = publicPath;
                sp.ClickUrl = string.IsNullOrWhiteSpace(b.ClickUrl) ? null : b.ClickUrl;
                sp.Tier = SponsorTier.Parceiro;
                sp.IsPublished = true;
                sp.SortOrder = b.Ordering;
                sp.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            log.LogInformation("Site {Site}: criados {Created}, atualizados {Updated}, imagens OK {ImgOk}, em falta {ImgMissing}",
                name, created, updated, imgOk, imgMissing);
        }

        log.LogInformation("=== Sponsors OK ===");
    }

    private static async Task<List<JoomlaBanner>> ReadJoomlaBanners(MySqlConnection conn, string prefix)
    {
        var list = new List<JoomlaBanner>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT id, name, alias, clickurl, state, ordering, params, catid
            FROM {prefix}banners
            WHERE state = 1
            ORDER BY ordering, id";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new JoomlaBanner(
                Id: r.GetInt32(0),
                Name: r.IsDBNull(1) ? "" : r.GetString(1),
                Alias: r.IsDBNull(2) ? "" : r.GetString(2),
                ClickUrl: r.IsDBNull(3) ? "" : r.GetString(3),
                State: r.GetInt32(4),
                Ordering: r.GetInt32(5),
                Params: r.IsDBNull(6) ? "" : r.GetString(6),
                CatId: r.GetInt32(7)
            ));
        }
        return list;
    }

    private static string? ExtractImageUrl(string paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            if (doc.RootElement.TryGetProperty("imageurl", out var el))
                return el.GetString();
        }
        catch (JsonException)
        {
            // params às vezes não são JSON válido; ignora
        }
        return null;
    }

    private static string Slugify(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "sponsor";
        var normalized = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private record JoomlaBanner(int Id, string Name, string Alias, string ClickUrl,
        int State, int Ordering, string Params, int CatId);
}
