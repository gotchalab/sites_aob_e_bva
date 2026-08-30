using System.Text.RegularExpressions;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AOB.Migrator.Commands.Bootstrap;

public class MigrateMenusCommand(
    AppDbContext db,
    MigratorOptions opts,
    SshTunnel tunnel,
    ILogger<MigrateMenusCommand> log)
{
    public async Task RunAsync()
    {
        log.LogInformation("=== Migrate menus ===");

        var src = new JoomlaSource(opts, tunnel);
        foreach (var (name, joomla) in src.Sites.Select(s => (s.Name, s.Joomla)))
        {
            log.LogInformation("--- Site: {Name} (SiteId={SiteId}) ---", name, joomla.TargetSiteId);

            var articleLookup = await db.Articles
                .Where(a => a.SiteId == joomla.TargetSiteId && a.LegacyId != null)
                .Select(a => new TargetRef(a.LegacyId!.Value, a.Id, a.Slug))
                .ToDictionaryAsync(x => x.LegacyId);

            var categoryLookup = await db.Categories
                .Where(c => c.SiteId == joomla.TargetSiteId && c.LegacyId != null && c.LegacyId < 100000)
                .Select(c => new TargetRef(c.LegacyId!.Value, c.Id, c.Slug))
                .ToDictionaryAsync(x => x.LegacyId);

            using var conn = src.Open(joomla);
            var joomlaItems = await ReadJoomlaMenus(conn, joomla.TablePrefix);
            log.LogInformation("Lidos {Count} menu items publicados", joomlaItems.Count);

            var existing = await db.MenuItems
                .Where(m => m.SiteId == joomla.TargetSiteId && m.LegacyId != null)
                .ToDictionaryAsync(m => m.LegacyId!.Value);

            var byJoomlaId = new Dictionary<int, MenuItem>(existing);

            foreach (var jm in joomlaItems.OrderBy(x => x.Level).ThenBy(x => x.Lft))
            {
                if (!byJoomlaId.TryGetValue(jm.Id, out var mi))
                {
                    mi = new MenuItem
                    {
                        SiteId = joomla.TargetSiteId,
                        LegacyId = jm.Id,
                    };
                    db.MenuItems.Add(mi);
                }
                mi.MenuType = string.IsNullOrWhiteSpace(jm.MenuType) ? "mainmenu" : jm.MenuType;
                mi.Title = jm.Title;
                mi.SortOrder = jm.Lft;
                mi.IsPublished = jm.Published == 1;

                var (targetType, targetId, url) = ResolveTarget(jm, articleLookup, categoryLookup);
                mi.TargetType = targetType;
                mi.TargetId = targetId;
                mi.Url = url;

                byJoomlaId[jm.Id] = mi;
            }

            foreach (var jm in joomlaItems)
            {
                if (byJoomlaId.TryGetValue(jm.Id, out var mi) &&
                    jm.ParentId > 1 && byJoomlaId.TryGetValue(jm.ParentId, out var parent))
                {
                    mi.Parent = parent;
                    mi.ParentId = parent.Id;
                }
            }

            await db.SaveChangesAsync();
            log.LogInformation("Site {Name}: {Total} menu items na BD",
                name, await db.MenuItems.CountAsync(m => m.SiteId == joomla.TargetSiteId));
        }

        log.LogInformation("=== Menus OK ===");
    }

    private static readonly Regex ArticleIdRx = new(@"[?&]id=(\d+)", RegexOptions.Compiled);
    private static readonly Regex CategoryIdRx = new(@"[?&]id=(\d+)", RegexOptions.Compiled);

    private record TargetRef(int LegacyId, int Id, string Slug);

    private static (MenuTargetType Type, int? Id, string? Url) ResolveTarget(
        JoomlaMenu jm,
        IReadOnlyDictionary<int, TargetRef> articles,
        IReadOnlyDictionary<int, TargetRef> categories)
    {
        var link = jm.Link ?? "";
        var type = jm.Type ?? "";

        if (type.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            return (link.StartsWith("http") ? MenuTargetType.External : MenuTargetType.Internal, null, link);
        }
        if (type.Equals("separator", StringComparison.OrdinalIgnoreCase))
        {
            return (MenuTargetType.Internal, null, null);
        }
        if (link.Contains("view=article"))
        {
            var m = ArticleIdRx.Match(link);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var legacyId) &&
                articles.TryGetValue(legacyId, out var art))
            {
                return (MenuTargetType.Article, art.Id, $"/artigos/{art.Slug}");
            }
        }
        if (link.Contains("view=category") || link.Contains("view=categories"))
        {
            var m = CategoryIdRx.Match(link);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var legacyId) &&
                categories.TryGetValue(legacyId, out var cat))
            {
                return (MenuTargetType.Category, cat.Id, $"/categoria/{cat.Slug}");
            }
        }

        return (MenuTargetType.Internal, null, link);
    }

    private static async Task<List<JoomlaMenu>> ReadJoomlaMenus(MySqlConnection conn, string prefix)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, menutype, title, alias, path, link, type, published,
                   parent_id, level, lft, rgt
            FROM {prefix}menu
            WHERE published >= 0
              AND client_id = 0
              AND menutype NOT IN ('main')
              AND id > 1
            ORDER BY lft
        """;
        var list = new List<JoomlaMenu>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new JoomlaMenu
            {
                Id = r.GetInt32(0),
                MenuType = r.GetString(1),
                Title = r.GetString(2),
                Alias = r.GetString(3),
                Path = r.GetString(4),
                Link = r.IsDBNull(5) ? "" : r.GetString(5),
                Type = r.GetString(6),
                Published = r.GetInt32(7),
                ParentId = r.GetInt32(8),
                Level = r.GetInt32(9),
                Lft = r.GetInt32(10),
                Rgt = r.GetInt32(11),
            });
        }
        return list;
    }

    private class JoomlaMenu
    {
        public int Id { get; set; }
        public string MenuType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Alias { get; set; } = "";
        public string Path { get; set; } = "";
        public string Link { get; set; } = "";
        public string Type { get; set; } = "";
        public int Published { get; set; }
        public int ParentId { get; set; }
        public int Level { get; set; }
        public int Lft { get; set; }
        public int Rgt { get; set; }
    }
}
