using System.Text.RegularExpressions;
using AOB.Core.Entities;
using AOB.Core.Utilities;
using AOB.Infrastructure.Persistence;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AOB.Migrator.Commands;

public class MigrateCategoriesCommand(
    AppDbContext db,
    MigratorOptions opts,
    SshTunnel tunnel,
    ILogger<MigrateCategoriesCommand> log)
{
    public async Task RunAsync()
    {
        log.LogInformation("=== Migrate categories ===");

        var src = new JoomlaSource(opts, tunnel);
        foreach (var (name, joomla) in src.Sites.Select(s => (s.Name, s.Joomla)))
        {
            log.LogInformation("--- Site: {Name} (target SiteId={SiteId}) ---", name, joomla.TargetSiteId);

            var siteExists = await db.Sites.AnyAsync(s => s.Id == joomla.TargetSiteId);
            if (!siteExists)
            {
                log.LogError("SiteId {SiteId} nao existe na BD. Corre 'seed' primeiro.", joomla.TargetSiteId);
                continue;
            }

            using var conn = src.Open(joomla);
            var joomlaCats = await ReadJoomlaCategories(conn, joomla.TablePrefix);
            log.LogInformation("Lidas {Count} categorias do Joomla (com_content)", joomlaCats.Count);

            var existing = await db.Categories
                .Where(c => c.SiteId == joomla.TargetSiteId && c.LegacyId != null)
                .ToDictionaryAsync(c => c.LegacyId!.Value);

            var byLegacyId = new Dictionary<int, Category>(existing);

            var ordered = TopologicalSort(joomlaCats);

            foreach (var jc in ordered)
            {
                if (!byLegacyId.TryGetValue(jc.Id, out var cat))
                {
                    cat = new Category
                    {
                        SiteId = joomla.TargetSiteId,
                        LegacyId = jc.Id,
                    };
                    db.Categories.Add(cat);
                }

                cat.Name = jc.Title;
                cat.Slug = string.IsNullOrWhiteSpace(jc.Alias) ? SlugHelper.Slugify(jc.Title) : jc.Alias;
                cat.Description = CleanDescription(jc.Description);
                cat.IsPublished = jc.Published == 1;
                cat.SortOrder = jc.Lft;
                cat.UpdatedAt = DateTime.UtcNow;

                if (jc.ParentJoomlaId > 1 && byLegacyId.TryGetValue(jc.ParentJoomlaId, out var parent))
                {
                    cat.Parent = parent;
                    cat.ParentId = parent.Id;
                }
                else
                {
                    cat.Parent = null;
                    cat.ParentId = null;
                }

                byLegacyId[jc.Id] = cat;
            }

            await db.SaveChangesAsync();
            var total = await db.Categories.CountAsync(c => c.SiteId == joomla.TargetSiteId);
            log.LogInformation("Site {Name}: {Total} categorias na BD ({New} novas)",
                name, total, ordered.Count - existing.Count);
        }

        log.LogInformation("=== Categorias OK ===");
    }

    private static async Task<List<JoomlaCat>> ReadJoomlaCategories(MySqlConnection conn, string prefix)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, parent_id, lft, rgt, level, title, alias, description, published
            FROM {prefix}categories
            WHERE extension = 'com_content'
              AND id > 1
            ORDER BY lft
        """;
        var list = new List<JoomlaCat>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new JoomlaCat
            {
                Id = r.GetInt32(0),
                ParentJoomlaId = r.GetInt32(1),
                Lft = r.GetInt32(2),
                Rgt = r.GetInt32(3),
                Level = r.GetInt32(4),
                Title = r.GetString(5),
                Alias = r.GetString(6),
                Description = r.IsDBNull(7) ? "" : r.GetString(7),
                Published = r.GetInt32(8),
            });
        }
        return list;
    }

    private static List<JoomlaCat> TopologicalSort(List<JoomlaCat> cats)
    {
        return cats.OrderBy(c => c.Level).ThenBy(c => c.Lft).ToList();
    }

    private static readonly HashSet<string> UnwrapTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "font", "span"
    };

    internal static string? CleanDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(raw);

        foreach (var node in doc.DocumentNode.SelectNodes("//*[@style]") ?? Enumerable.Empty<HtmlNode>())
            node.Attributes.Remove("style");
        foreach (var node in doc.DocumentNode.SelectNodes("//*[@color]") ?? Enumerable.Empty<HtmlNode>())
            node.Attributes.Remove("color");
        foreach (var node in doc.DocumentNode.SelectNodes("//*[@bgcolor]") ?? Enumerable.Empty<HtmlNode>())
            node.Attributes.Remove("bgcolor");
        foreach (var node in doc.DocumentNode.SelectNodes("//*[@face]") ?? Enumerable.Empty<HtmlNode>())
            node.Attributes.Remove("face");

        // Unwrap tags visuais (font, span) mantendo o conteúdo. Iteramos até estabilizar.
        while (true)
        {
            var toUnwrap = (doc.DocumentNode.SelectNodes("//*") ?? Enumerable.Empty<HtmlNode>())
                .Where(n => UnwrapTags.Contains(n.Name))
                .ToList();
            if (toUnwrap.Count == 0) break;
            foreach (var n in toUnwrap)
            {
                foreach (var child in n.ChildNodes.ToList())
                    n.ParentNode.InsertBefore(child, n);
                n.Remove();
            }
        }

        var html = doc.DocumentNode.InnerHtml;
        html = Regex.Replace(html, @"\s+", " ");
        html = Regex.Replace(html, @">\s+<", "><");
        html = html.Trim();
        return string.IsNullOrWhiteSpace(html) ? null : html;
    }

    private class JoomlaCat
    {
        public int Id { get; set; }
        public int ParentJoomlaId { get; set; }
        public int Lft { get; set; }
        public int Rgt { get; set; }
        public int Level { get; set; }
        public string Title { get; set; } = "";
        public string Alias { get; set; } = "";
        public string Description { get; set; } = "";
        public int Published { get; set; }
    }
}
