using System.Text;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands.Bootstrap;

/// <summary>
/// Gera infra/nginx/redirects.map com URLs legacy do Joomla -> novos slugs.
/// Cada domínio tem o seu ficheiro para Nginx aplicar via `map`.
/// </summary>
public class GenerateRedirectsCommand(
    AppDbContext db,
    MigratorOptions opts,
    ILogger<GenerateRedirectsCommand> log)
{
    public async Task RunAsync()
    {
        log.LogInformation("=== Generate redirects ===");
        var outDir = ResolveOutputDir();
        Directory.CreateDirectory(outDir);

        foreach (var joomla in new[] { opts.Joomla.Aob, opts.Joomla.Bva })
        {
            var site = await db.Sites.FindAsync(joomla.TargetSiteId);
            if (site is null)
            {
                log.LogWarning("Site {Id} nao existe — a saltar", joomla.TargetSiteId);
                continue;
            }

            var articles = await db.Articles
                .Where(a => a.SiteId == site.Id && a.LegacyId != null)
                .Select(a => new { a.LegacyId, a.Slug })
                .ToListAsync();

            var categories = await db.Categories
                .Where(c => c.SiteId == site.Id && c.LegacyId != null && c.LegacyId < 100000)
                .Select(c => new { c.LegacyId, c.Slug })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"# Legacy Joomla -> new URLs for {site.Domain}");
            sb.AppendLine($"# Generated {DateTime.UtcNow:O} — DO NOT EDIT BY HAND");
            sb.AppendLine();
            sb.AppendLine("# Use in nginx:");
            sb.AppendLine("#   map $request_uri $legacy_redirect {");
            sb.AppendLine("#     default \"\";");
            sb.AppendLine($"#     include /etc/nginx/redirects.{site.Slug}.map;");
            sb.AppendLine("#   }");
            sb.AppendLine();

            foreach (var a in articles.OrderBy(a => a.LegacyId))
                sb.AppendLine($"~^/index\\.php\\?option=com_content&view=article&id={a.LegacyId}(?:&.*)?$   /artigos/{a.Slug};");

            foreach (var c in categories.OrderBy(c => c.LegacyId))
                sb.AppendLine($"~^/index\\.php\\?option=com_content&view=category&id={c.LegacyId}(?:&.*)?$   /categoria/{c.Slug};");

            var path = Path.Combine(outDir, $"redirects.{site.Slug}.map");
            await File.WriteAllTextAsync(path, sb.ToString());
            log.LogInformation("Escrito {Path} ({Articles} artigos + {Cats} categorias)",
                path, articles.Count, categories.Count);
        }

        log.LogInformation("=== Redirects OK ===");
    }

    private static string ResolveOutputDir()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidate is not null)
        {
            var infra = Path.Combine(candidate.FullName, "infra", "nginx");
            if (Directory.Exists(Path.Combine(candidate.FullName, "infra")))
                return infra;
            candidate = candidate.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "redirects-out");
    }
}
