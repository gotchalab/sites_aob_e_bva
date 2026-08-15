using System.Text.RegularExpressions;
using AOB.Application.Content;
using AOB.Application.Contracts;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AOB.Api.Endpoints;

public static class PublicEndpoints
{
    private static readonly Regex FirstImgSrcRx =
        new(@"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static RouteGroupBuilder MapPublic(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sites/{siteSlug}").WithTags("Public");

        g.MapGet("", GetSite);
        g.MapGet("/menu", GetMenu);
        g.MapGet("/categories", GetCategories);
        g.MapGet("/articles", ListArticles);
        g.MapGet("/articles/{slug}", GetArticle);
        g.MapPost("/articles/{slug}/view", RegisterArticleView);
        g.MapGet("/downloads", ListDownloads);
        g.MapGet("/downloads/{slug}", GetDownload);
        g.MapPost("/downloads/{slug}/hit", RegisterDownloadHit);
        g.MapGet("/sponsors", ListSponsors);
        return g;
    }

    private static async Task<Results<Ok<SiteDto>, NotFound>> GetSite(
        string siteSlug, AppDbContext db)
    {
        var s = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        return s is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new SiteDto(s.Id, s.Slug, s.Name, s.Domain, s.Description,
                UrlPathEncoder.EncodeUploadPath(s.LogoUrl), s.PrimaryColor, s.ContactEmail,
                s.Tagline, s.HomeConfig, s.Announcement));
    }

    private static async Task<Results<Ok<List<MenuItemDto>>, NotFound>> GetMenu(
        string siteSlug, string? type, AppDbContext db)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        var menuType = string.IsNullOrWhiteSpace(type) ? "mainmenu" : type;
        var items = await db.MenuItems.AsNoTracking()
            .Where(m => m.SiteId == site.Id && m.MenuType == menuType && m.IsPublished)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        var byId = items.ToDictionary(m => m.Id, m => new MenuItemDto(
            m.Id, m.Title, m.Url, m.TargetType.ToString(), m.TargetId, m.SortOrder, m.OpenInNewTab, new()));

        var roots = new List<MenuItemDto>();
        foreach (var m in items)
        {
            var dto = byId[m.Id];
            if (m.ParentId is int pid && byId.TryGetValue(pid, out var parent))
                parent.Children.Add(dto);
            else
                roots.Add(dto);
        }
        return TypedResults.Ok(roots);
    }

    private static async Task<Results<Ok<List<CategoryTreeDto>>, NotFound>> GetCategories(
        string siteSlug, AppDbContext db, string? kind = null)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        var q = db.Categories.AsNoTracking()
            .Where(c => c.SiteId == site.Id && c.IsPublished);

        q = kind switch
        {
            "downloads" => q.Where(c => c.Slug.StartsWith("dl-")),
            "articles" or null or "" => q.Where(c => !c.Slug.StartsWith("dl-")),
            _ => q,
        };

        var cats = await q.OrderBy(c => c.SortOrder).ToListAsync();
        var byId = cats.ToDictionary(c => c.Id, c => new CategoryTreeDto(
            c.Id, c.Slug, c.Name, c.Description, c.SortOrder, new()));

        var roots = new List<CategoryTreeDto>();
        foreach (var c in cats)
        {
            var dto = byId[c.Id];
            if (c.ParentId is int pid && byId.TryGetValue(pid, out var parent))
                parent.Children.Add(dto);
            else
                roots.Add(dto);
        }
        return TypedResults.Ok(roots);
    }

    private static async Task<Results<Ok<PagedResult<ArticleSummaryDto>>, NotFound>> ListArticles(
        string siteSlug, AppDbContext db,
        string? category = null, int page = 1, int pageSize = 20,
        int? offset = null, int? limit = null,
        string? search = null, bool? featured = null)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        // "offset" e "limit" tomam precedencia sobre "page/pageSize" (paginacao arbitraria).
        var effectiveTake = Math.Clamp(limit ?? pageSize, 1, 100);
        var effectiveSkip = offset.HasValue
            ? Math.Max(0, offset.Value)
            : Math.Max(0, (Math.Max(1, page) - 1) * effectiveTake);

        var q = db.Articles.AsNoTracking()
            .Include(a => a.Category)
            .Where(a => a.SiteId == site.Id && a.IsPublished);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(a => a.Category.Slug == category);

        if (featured == true)
            q = q.Where(a => a.IsFeatured);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            q = q.Where(a => EF.Functions.ILike(a.Title, s) ||
                             (a.Excerpt != null && EF.Functions.ILike(a.Excerpt, s)));
        }

        var total = await q.CountAsync();
        var raw = await q.OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Skip(effectiveSkip)
            .Take(effectiveTake)
            .Select(a => new { a.Id, a.Slug, a.Title, a.Excerpt, a.CoverImagePath, a.Content, a.PublishedAt, CategorySlug = a.Category.Slug, CategoryName = a.Category.Name, a.IsFeatured })
            .ToListAsync();
        var items = raw
            .Select(a =>
            {
                var cover = a.CoverImagePath;
                if (string.IsNullOrEmpty(cover) && !string.IsNullOrEmpty(a.Content))
                {
                    var m = FirstImgSrcRx.Match(a.Content);
                    if (m.Success) cover = m.Groups[1].Value;
                }
                return new ArticleSummaryDto(a.Id, a.Slug, a.Title, a.Excerpt,
                    UrlPathEncoder.EncodeUploadPath(cover), a.PublishedAt, a.CategorySlug, a.CategoryName, a.IsFeatured);
            })
            .ToList();

        // Devolve page/pageSize coerentes com o skip usado para consumidores que so olham para page.
        var reportedPage = (effectiveSkip / effectiveTake) + 1;
        return TypedResults.Ok(new PagedResult<ArticleSummaryDto>(total, reportedPage, effectiveTake, items));
    }

    private static async Task<Results<Ok<List<SponsorDto>>, NotFound>> ListSponsors(
        string siteSlug, AppDbContext db)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        var raw = await db.Sponsors.AsNoTracking()
            .Where(s => s.SiteId == site.Id && s.IsPublished)
            .OrderBy(s => s.Tier).ThenBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new SponsorDto(s.Id, s.Name, s.Slug, s.LogoPath, s.ClickUrl,
                s.Tier.ToString(), s.SortOrder))
            .ToListAsync();
        var items = raw
            .Select(s => s with { LogoPath = UrlPathEncoder.EncodeUploadPath(s.LogoPath) ?? s.LogoPath })
            .ToList();

        return TypedResults.Ok(items);
    }

    private static async Task<Results<Ok<ArticleDetailDto>, NotFound>> GetArticle(
        string siteSlug, string slug, AppDbContext db, ShortcodeExpander shortcodes)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        var a = await db.Articles.AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.SiteId == site.Id && x.Slug == slug && x.IsPublished);

        if (a is null) return TypedResults.NotFound();

        var expandedContent = await shortcodes.ExpandAsync(a.Content, site.Id, site.Slug);

        return TypedResults.Ok(new ArticleDetailDto(
            a.Id, a.Slug, a.Title, a.Excerpt, expandedContent,
            UrlPathEncoder.EncodeUploadPath(a.CoverImagePath),
            a.PublishedAt, a.Category.Slug, a.Category.Name,
            a.MetaTitle, a.MetaDescription, a.ViewCount, a.Tags));
    }

    private static async Task<Results<NoContent, NotFound>> RegisterArticleView(
        string siteSlug, string slug, HttpContext ctx, AppDbContext db, IMemoryCache cache)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var key = $"view:{site.Id}:{slug}:{ip}";
        if (cache.TryGetValue(key, out _))
            return TypedResults.NoContent();

        var updated = await db.Articles
            .Where(x => x.SiteId == site.Id && x.Slug == slug && x.IsPublished)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ViewCount, x => x.ViewCount + 1));

        if (updated == 0) return TypedResults.NotFound();

        cache.Set(key, true, TimeSpan.FromMinutes(30));
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<PagedResult<DownloadSummaryDto>>, NotFound>> ListDownloads(
        string siteSlug, AppDbContext db,
        string? category = null, int page = 1, int pageSize = 20, string? search = null)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = db.Downloads.AsNoTracking()
            .Include(d => d.Category)
            .Where(d => d.SiteId == site.Id && d.IsPublished);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(d => d.Category!.Slug == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            q = q.Where(d => EF.Functions.ILike(d.Title, s));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(d => d.PublishedAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DownloadSummaryDto(d.Id, d.Slug, d.Title, d.Description,
                d.FileName, d.FileSize, d.MimeType, d.PublishedAt, d.DownloadCount,
                d.Category != null ? d.Category.Slug : null,
                d.Category != null ? d.Category.Name : null))
            .ToListAsync();

        return TypedResults.Ok(new PagedResult<DownloadSummaryDto>(total, page, pageSize, items));
    }

    private static async Task<Results<Ok<DownloadDetailDto>, NotFound>> GetDownload(
        string siteSlug, string slug, AppDbContext db)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        var d = await db.Downloads.AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.SiteId == site.Id && x.Slug == slug && x.IsPublished);

        if (d is null) return TypedResults.NotFound();

        return TypedResults.Ok(new DownloadDetailDto(d.Id, d.Slug, d.Title, d.Description,
            d.FileName, d.StoragePath, d.FileSize, d.MimeType, d.PublishedAt,
            d.DownloadCount,
            d.Category?.Slug, d.Category?.Name));
    }

    private static async Task<Results<NoContent, NotFound>> RegisterDownloadHit(
        string siteSlug, string slug, HttpContext ctx, AppDbContext db, IMemoryCache cache)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == siteSlug);
        if (site is null) return TypedResults.NotFound();

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var key = $"dl-hit:{site.Id}:{slug}:{ip}";
        if (cache.TryGetValue(key, out _))
            return TypedResults.NoContent();

        var updated = await db.Downloads
            .Where(x => x.SiteId == site.Id && x.Slug == slug && x.IsPublished)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DownloadCount, x => x.DownloadCount + 1));

        if (updated == 0) return TypedResults.NotFound();

        cache.Set(key, true, TimeSpan.FromMinutes(30));
        return TypedResults.NoContent();
    }
}
