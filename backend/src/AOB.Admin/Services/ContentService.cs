using AOB.Core.Entities;
using AOB.Core.Utilities;
using AOB.Infrastructure.Persistence;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

/// <summary>
/// CRUD helpers para o backoffice. Sanitiza HTML ao guardar.
/// </summary>
public class ContentService(AppDbContext db)
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    // Strip style="…" em <table>/<tr>/<thead>/<tbody> — elementos estruturais que
    // nao devem ter estilo inline (cor de fundo, borda) num tema light-only.
    // Pastes de dark-mode injectam ai "background-color: rgba(25,26,27,1)" que
    // torna a tabela ilegivel. <td>/<th> preservam style (podem ter text-align,
    // width, etc. legitimos definidos pelo autor).
    private static readonly System.Text.RegularExpressions.Regex TableWrapperStyleRx =
        new(@"(<(?:table|tr|thead|tbody)\b[^>]*?)\s+style\s*=\s*""[^""]*""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    // Nos <td>/<th> so remove propriedades cromaticas problematicas, preservando
    // outras (text-align, width, vertical-align, padding).
    private static readonly System.Text.RegularExpressions.Regex CellColorPropRx =
        new(@"(?:background-color|background|color|border-color|border)\s*:\s*[^;""]+;?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex CellStyleRx =
        new(@"(<(?:td|th)\b[^>]*?\s)style\s*=\s*""([^""]*)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string CleanTableStyles(string html)
    {
        if (string.IsNullOrEmpty(html) || !html.Contains("<t", StringComparison.OrdinalIgnoreCase)) return html;
        // 1) Remove style inteiro de <table>/<tr>/<thead>/<tbody>.
        html = TableWrapperStyleRx.Replace(html, "$1");
        // 2) Em <td>/<th>, remove apenas propriedades cromaticas do style. Se sobrar
        // string vazia, remove o atributo inteiro.
        html = CellStyleRx.Replace(html, m =>
        {
            var prefix = m.Groups[1].Value;
            var cleaned = CellColorPropRx.Replace(m.Groups[2].Value, "").Trim();
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @";\s*;", ";").Trim(';', ' ');
            return string.IsNullOrEmpty(cleaned) ? prefix.TrimEnd() : $"{prefix}style=\"{cleaned}\"";
        });
        return html;
    }

    public IQueryable<Site> Sites() => db.Sites.AsNoTracking().OrderBy(s => s.Slug);

    public Task<List<Category>> ListCategories(int siteId, bool includeDownloads = false) =>
        db.Categories.AsNoTracking()
            .Where(c => c.SiteId == siteId &&
                (includeDownloads || !c.Slug.StartsWith("dl-")))
            .OrderBy(c => c.Slug.StartsWith("dl-") ? 1 : 0)
            .ThenBy(c => c.SortOrder)
            .ToListAsync();

    /// <summary>
    /// Devolve as categorias do site ordenadas em árvore (pai → filhos por SortOrder),
    /// já com <see cref="CategoryNode.Depth"/> calculado para indentação na UI.
    /// </summary>
    public async Task<List<CategoryNode>> ListCategoryTree(int siteId, bool includeDownloads = false)
    {
        var flat = await ListCategories(siteId, includeDownloads);
        var byParent = flat.GroupBy(c => c.ParentId ?? 0)
                           .ToDictionary(g => g.Key, g => g.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList());
        var result = new List<CategoryNode>(flat.Count);
        void Walk(int parentId, int depth)
        {
            if (!byParent.TryGetValue(parentId, out var kids)) return;
            foreach (var c in kids)
            {
                result.Add(new CategoryNode(c, depth));
                Walk(c.Id, depth + 1);
            }
        }
        Walk(0, 0);
        // categorias com ParentId apontando para pai fora do conjunto (ex: filtrado por dl-)
        // — apanhar como raízes para não desaparecerem
        var seen = new HashSet<int>(result.Select(n => n.Category.Id));
        foreach (var c in flat.Where(c => !seen.Contains(c.Id)))
        {
            result.Add(new CategoryNode(c, 0));
            Walk(c.Id, 1);
        }
        return result;
    }

    /// <summary>Ids da categoria e de todos os seus descendentes (para evitar ciclos ao escolher pai).</summary>
    public async Task<HashSet<int>> GetDescendantIds(int siteId, int categoryId)
    {
        var flat = await db.Categories.AsNoTracking()
            .Where(c => c.SiteId == siteId)
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync();
        var byParent = flat.GroupBy(c => c.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var set = new HashSet<int> { categoryId };
        var stack = new Stack<int>();
        stack.Push(categoryId);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!byParent.TryGetValue(id, out var kids)) continue;
            foreach (var k in kids)
            {
                if (set.Add(k)) stack.Push(k);
            }
        }
        return set;
    }

    public Task<Category?> GetCategory(int id) =>
        db.Categories.FirstOrDefaultAsync(c => c.Id == id);

    public async Task SaveCategory(Category cat)
    {
        if (string.IsNullOrWhiteSpace(cat.Slug)) cat.Slug = SlugHelper.Slugify(cat.Name);
        cat.UpdatedAt = DateTime.UtcNow;
        if (cat.Id == 0) db.Categories.Add(cat);
        else db.Categories.Update(cat);
        await db.SaveChangesAsync();
    }

    public async Task DeleteCategory(int id)
    {
        var c = await db.Categories.FindAsync(id);
        if (c is null) return;
        db.Categories.Remove(c);
        await db.SaveChangesAsync();
    }

    public Task<List<Article>> ListArticles(int siteId, string? search, int page, int pageSize) =>
        BuildArticleQuery(siteId, search)
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

    public Task<int> CountArticles(int siteId, string? search) =>
        BuildArticleQuery(siteId, search).CountAsync();

    private IQueryable<Article> BuildArticleQuery(int siteId, string? search)
    {
        var q = db.Articles.AsNoTracking()
            .Include(a => a.Category)
            .Where(a => a.SiteId == siteId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            q = q.Where(a => EF.Functions.ILike(a.Title, s));
        }
        return q;
    }

    public Task<Article?> GetArticle(int id) =>
        db.Articles.Include(a => a.Category).FirstOrDefaultAsync(a => a.Id == id);

    private static readonly System.Text.RegularExpressions.Regex FirstImgSrcRx =
        new(@"<img[^>]+src=""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task SaveArticle(Article art)
    {
        if (string.IsNullOrWhiteSpace(art.Slug)) art.Slug = SlugHelper.Slugify(art.Title);
        art.Content = CleanTableStyles(Sanitizer.Sanitize(art.Content ?? ""));
        if (string.IsNullOrWhiteSpace(art.CoverImagePath) && !string.IsNullOrWhiteSpace(art.Content))
        {
            var m = FirstImgSrcRx.Match(art.Content);
            if (m.Success) art.CoverImagePath = m.Groups[1].Value;
        }
        art.UpdatedAt = DateTime.UtcNow;
        if (art.IsPublished && art.PublishedAt is null) art.PublishedAt = DateTime.UtcNow;
        if (art.Id == 0)
        {
            art.CreatedAt = DateTime.UtcNow;
            db.Articles.Add(art);
        }
        else
        {
            db.Articles.Update(art);
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteArticle(int id)
    {
        var a = await db.Articles.FindAsync(id);
        if (a is null) return;
        db.Articles.Remove(a);
        await db.SaveChangesAsync();
    }

    public Task<List<Download>> ListDownloads(int siteId, string? search, int page, int pageSize, int? categoryId = null) =>
        BuildDownloadQuery(siteId, search, categoryId)
            .OrderByDescending(d => d.PublishedAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

    public Task<int> CountDownloads(int siteId, string? search, int? categoryId = null) =>
        BuildDownloadQuery(siteId, search, categoryId).CountAsync();

    private IQueryable<Download> BuildDownloadQuery(int siteId, string? search, int? categoryId)
    {
        var q = db.Downloads.AsNoTracking()
            .Include(d => d.Category)
            .Where(d => d.SiteId == siteId);
        if (categoryId is int cid)
            q = q.Where(d => d.CategoryId == cid);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            q = q.Where(d => EF.Functions.ILike(d.Title, s) || EF.Functions.ILike(d.FileName, s));
        }
        return q;
    }

    public Task<Download?> GetDownload(int id) =>
        db.Downloads.Include(d => d.Category).FirstOrDefaultAsync(d => d.Id == id);

    public async Task SaveDownload(Download dl)
    {
        if (string.IsNullOrWhiteSpace(dl.Slug)) dl.Slug = SlugHelper.Slugify(dl.Title);
        dl.UpdatedAt = DateTime.UtcNow;
        if (dl.IsPublished && dl.PublishedAt is null) dl.PublishedAt = DateTime.UtcNow;
        if (dl.Id == 0)
        {
            dl.CreatedAt = DateTime.UtcNow;
            db.Downloads.Add(dl);
        }
        else
        {
            db.Downloads.Update(dl);
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteDownload(int id)
    {
        var d = await db.Downloads.FindAsync(id);
        if (d is null) return;
        db.Downloads.Remove(d);
        await db.SaveChangesAsync();
    }

    public async Task<(int articles, int downloads, int categories, int menuItems)> GetStats(int siteId)
    {
        return (
            await db.Articles.CountAsync(a => a.SiteId == siteId),
            await db.Downloads.CountAsync(d => d.SiteId == siteId),
            await db.Categories.CountAsync(c => c.SiteId == siteId),
            await db.MenuItems.CountAsync(m => m.SiteId == siteId)
        );
    }

    public record CategoryNode(Category Category, int Depth)
    {
        public string IndentedName => Depth == 0 ? Category.Name : string.Concat(Enumerable.Repeat("— ", Depth)) + Category.Name;
        /// Como <see cref="IndentedName"/> mas sufixado com o slug entre parentesis
        /// para desambiguar quando duas categorias diferentes partilham o mesmo nome
        /// (comum entre as arvores de artigos e de downloads, ex.: "BVA Masters" +
        /// "BVA Masters" com slug "dl-bva-masters").
        public string IndentedNameWithSlug => $"{IndentedName} ({Category.Slug})";
    }

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
}
