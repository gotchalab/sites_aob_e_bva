using System.Text.RegularExpressions;
using AOB.Core.Entities;
using AOB.Core.Utilities;
using AOB.Infrastructure.Persistence;
using Ganss.Xss;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AOB.Migrator.Commands.Bootstrap;

public class MigrateArticlesCommand(
    AppDbContext db,
    MigratorOptions opts,
    SshTunnel tunnel,
    ILogger<MigrateArticlesCommand> log)
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    public async Task RunAsync()
    {
        log.LogInformation("=== Migrate articles ===");

        var src = new JoomlaSource(opts, tunnel);
        foreach (var (name, joomla) in src.Sites.Select(s => (s.Name, s.Joomla)))
        {
            var siteSlug = name;
            log.LogInformation("--- Site: {Name} (SiteId={SiteId}) ---", name, joomla.TargetSiteId);

            var categoryLookup = await db.Categories
                .Where(c => c.SiteId == joomla.TargetSiteId && c.LegacyId != null)
                .ToDictionaryAsync(c => c.LegacyId!.Value, c => c.Id);

            if (categoryLookup.Count == 0)
            {
                log.LogError("Nao ha categorias migradas. Corre 'migrate categories' primeiro.");
                continue;
            }

            using var conn = src.Open(joomla);
            var joomlaArticles = await ReadJoomlaArticles(conn, joomla.TablePrefix);
            log.LogInformation("Lidos {Count} artigos do Joomla", joomlaArticles.Count);

            var existing = await db.Articles
                .Where(a => a.SiteId == joomla.TargetSiteId && a.LegacyId != null)
                .ToDictionaryAsync(a => a.LegacyId!.Value);

            var joomlaIds = joomlaArticles.Select(j => j.Id).ToHashSet();
            var otherSlugs = await db.Articles
                .Where(a => a.SiteId == joomla.TargetSiteId && (a.LegacyId == null || !joomlaIds.Contains(a.LegacyId.Value)))
                .Select(a => a.Slug)
                .ToListAsync();
            var slugSeen = new HashSet<string>(otherSlugs, StringComparer.OrdinalIgnoreCase);

            int created = 0, updated = 0, skipped = 0;
            foreach (var ja in joomlaArticles)
            {
                if (!categoryLookup.TryGetValue(ja.CatId, out var newCatId))
                {
                    log.LogWarning("Artigo id={Id} '{Title}' aponta para categoria Joomla {CatId} nao migrada — SKIP",
                        ja.Id, ja.Title, ja.CatId);
                    skipped++;
                    continue;
                }

                var rawHtml = string.IsNullOrEmpty(ja.FullText)
                    ? ja.IntroText
                    : ja.IntroText + "\n" + ja.FullText;

                var (safeHtml, coverImage) = ProcessHtml(rawHtml, ja.ImagesJson, siteSlug);

                if (!existing.TryGetValue(ja.Id, out var art))
                {
                    art = new Article
                    {
                        SiteId = joomla.TargetSiteId,
                        LegacyId = ja.Id,
                        CreatedAt = ja.Created,
                    };
                    db.Articles.Add(art);
                    created++;
                }
                else
                {
                    updated++;
                }

                art.CategoryId = newCatId;
                var baseSlug = string.IsNullOrWhiteSpace(ja.Alias) ? SlugHelper.Slugify(ja.Title, 300) : ja.Alias;
                var finalSlug = baseSlug;
                if (!slugSeen.Add(finalSlug))
                {
                    finalSlug = $"{baseSlug}-{ja.Id}";
                    slugSeen.Add(finalSlug);
                    log.LogWarning("Slug duplicado '{Base}' — desambiguado para '{Final}' (article id={Id})",
                        baseSlug, finalSlug, ja.Id);
                }
                art.Slug = finalSlug;
                art.Title = ja.Title;
                art.Content = safeHtml;
                art.Excerpt = ExtractExcerpt(safeHtml, 400);
                art.CoverImagePath = coverImage;
                art.IsPublished = ja.State == 1;
                art.PublishedAt = ja.PublishUp == DateTime.MinValue ? ja.Created : ja.PublishUp;
                art.MetaTitle = Trim(ja.MetaTitle, 200);
                art.MetaDescription = Trim(ja.MetaDesc, 500);
                art.ViewCount = ja.Hits;
                art.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            log.LogInformation("Site {Name}: {Created} novos, {Updated} atualizados, {Skipped} skipped",
                name, created, updated, skipped);
        }

        log.LogInformation("=== Artigos OK ===");
    }

    private static (string Html, string? CoverImage) ProcessHtml(string rawHtml, string imagesJson, string siteSlug)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
            return ("", ParseCoverImage(imagesJson, siteSlug));

        var doc = new HtmlDocument();
        doc.LoadHtml(rawHtml);

        foreach (var img in doc.DocumentNode.SelectNodes("//img[@src]") ?? Enumerable.Empty<HtmlNode>())
        {
            var src = img.GetAttributeValue("src", "");
            img.SetAttributeValue("src", RewriteMediaPath(src, siteSlug));
            img.SetAttributeValue("loading", "lazy");
        }
        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.GetAttributeValue("href", "");
            a.SetAttributeValue("href", RewriteMediaPath(href, siteSlug));
        }

        var rewritten = doc.DocumentNode.InnerHtml;
        var safe = Sanitizer.Sanitize(rewritten);

        var cover = ParseCoverImage(imagesJson, siteSlug);
        if (string.IsNullOrWhiteSpace(cover))
        {
            var firstImg = doc.DocumentNode.SelectSingleNode("//img[@src]");
            if (firstImg != null)
                cover = firstImg.GetAttributeValue("src", null!);
        }
        return (safe, cover);
    }

    private static readonly Regex ImagePathRewrite = new(
        @"^/?(?<kind>images|phocadownload)/(?<rest>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string RewriteMediaPath(string path, string siteSlug)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.StartsWith("http://") || path.StartsWith("https://") ||
            path.StartsWith("mailto:") || path.StartsWith("#") ||
            path.StartsWith("data:") || path.StartsWith("javascript:"))
            return path;

        var m = ImagePathRewrite.Match(path);
        if (!m.Success) return path;

        var kind = m.Groups["kind"].Value.ToLowerInvariant();
        var rest = m.Groups["rest"].Value.TrimStart('/');
        return kind switch
        {
            "images" => $"/uploads/{siteSlug}/images/{rest}",
            "phocadownload" => $"/uploads/{siteSlug}/downloads/{rest}",
            _ => path,
        };
    }

    private static string? ParseCoverImage(string imagesJson, string siteSlug)
    {
        if (string.IsNullOrWhiteSpace(imagesJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(imagesJson);
            var root = doc.RootElement;
            foreach (var key in new[] { "image_fulltext", "image_intro" })
            {
                if (root.TryGetProperty(key, out var el))
                {
                    var v = el.GetString();
                    if (!string.IsNullOrWhiteSpace(v))
                        return RewriteMediaPath(v, siteSlug);
                }
            }
        }
        catch { }
        return null;
    }

    private static string? ExtractExcerpt(string html, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var text = doc.DocumentNode.InnerText;
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= maxLen) return text;
        var cut = text[..maxLen];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > 100 ? cut[..lastSpace] : cut).TrimEnd() + "…";
    }

    private static string? Trim(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Length > max ? s[..max] : s);

    private static HtmlSanitizer BuildSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedSchemes.Add("mailto");
        s.AllowedSchemes.Add("tel");
        foreach (var t in new[] { "iframe", "figure", "figcaption", "video", "source", "audio" })
            s.AllowedTags.Add(t);
        foreach (var a in new[] { "loading", "allowfullscreen", "frameborder", "controls", "poster", "type" })
            s.AllowedAttributes.Add(a);
        return s;
    }

    private static async Task<List<JoomlaArticle>> ReadJoomlaArticles(MySqlConnection conn, string prefix)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, title, alias, introtext, `fulltext`, state, catid,
                   created, modified, publish_up, images, metakey, metadesc, hits
            FROM `{prefix}content`
            WHERE state IN (0,1)
            ORDER BY id
        """;
        var list = new List<JoomlaArticle>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new JoomlaArticle
            {
                Id = r.GetInt32(0),
                Title = r.GetString(1),
                Alias = r.GetString(2),
                IntroText = r.IsDBNull(3) ? "" : r.GetString(3),
                FullText = r.IsDBNull(4) ? "" : r.GetString(4),
                State = r.GetInt32(5),
                CatId = r.GetInt32(6),
                Created = SafeDate(r, 7),
                Modified = SafeDate(r, 8),
                PublishUp = SafeDate(r, 9),
                ImagesJson = r.IsDBNull(10) ? "" : r.GetString(10),
                MetaTitle = null,
                MetaDesc = r.IsDBNull(12) ? "" : r.GetString(12),
                Hits = r.GetInt32(13),
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

    private class JoomlaArticle
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Alias { get; set; } = "";
        public string IntroText { get; set; } = "";
        public string FullText { get; set; } = "";
        public int State { get; set; }
        public int CatId { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public DateTime PublishUp { get; set; }
        public string ImagesJson { get; set; } = "";
        public string? MetaTitle { get; set; }
        public string MetaDesc { get; set; } = "";
        public int Hits { get; set; }
    }
}
