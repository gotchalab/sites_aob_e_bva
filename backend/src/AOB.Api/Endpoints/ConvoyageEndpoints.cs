using AOB.Application.Contracts;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AOB.Api.Endpoints;

public static class ConvoyageEndpoints
{
    public static IEndpointRouteBuilder MapConvoyage(this IEndpointRouteBuilder app)
    {
        // Public
        app.MapGet("/api/sites/{siteSlug}/convoyage/active-year", GetActiveYear)
            .WithTags("Convoyage");

        // Admin
        var admin = app.MapGroup("/api/admin/sites/{siteSlug}/convoyage")
            .WithTags("Convoyage Admin")
            .RequireAuthorization("Admin");

        admin.MapGet("/years", ListYears);
        admin.MapPost("/years", CreateYear);
        admin.MapPut("/years/{id}/activate", ActivateYear);
        admin.MapGet("/years/{id}/inscricoes", ListInscricoes);
        admin.MapGet("/years/{yearId}/collection-points", ListCollectionPoints);
        admin.MapPost("/years/{yearId}/collection-points", AddCollectionPoint);
        admin.MapDelete("/collection-points/{id}", DeleteCollectionPoint);

        return app;
    }

    // ── Public ───────────────────────────────────────────────────────────────

    private static async Task<Results<Ok<ConvoyageActiveYearDto>, NotFound>> GetActiveYear(
        string siteSlug, AppDbContext db, CancellationToken ct)
    {
        var year = await db.ConvoyageYears
            .Include(y => y.CollectionPoints.OrderBy(p => p.SortOrder))
            .Include(y => y.RegulamentoDownload)
            .Where(y => y.Site.Slug == siteSlug && y.IsActive)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (year is null) return TypedResults.NotFound();

        // Regulamento so exposto se o download existir E estiver publicado.
        var reg = year.RegulamentoDownload;
        var regUrl  = reg?.IsPublished == true ? reg.StoragePath : null;
        var regName = reg?.IsPublished == true ? reg.FileName    : null;

        return TypedResults.Ok(new ConvoyageActiveYearDto(
            year.Id,
            year.Year,
            year.Description,
            year.CollectionPoints
                .Select(p => new ConvoyageCollectionPointDto(p.Id, p.Name, p.Location))
                .ToList(),
            year.RegistrationClosesAt,
            regUrl,
            regName));
    }

    // ── Admin ─────────────────────────────────────────────────────────────────

    private static async Task<Results<Ok<List<ConvoyageYearAdminDto>>, NotFound>> ListYears(
        string siteSlug, AppDbContext db, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == siteSlug, ct);
        if (site is null) return TypedResults.NotFound();

        var years = await db.ConvoyageYears
            .Where(y => y.SiteId == site.Id)
            .OrderByDescending(y => y.Year)
            .Select(y => new ConvoyageYearAdminDto(
                y.Id, y.Year, y.IsActive, y.Description, y.CreatedAt,
                y.Submissions.Count(s => s.FormType == FormType.InscricaoConvoyage)))
            .AsNoTracking()
            .ToListAsync(ct);

        return TypedResults.Ok(years);
    }

    private static async Task<Results<Ok<ConvoyageYearAdminDto>, BadRequest<string>, NotFound>> CreateYear(
        string siteSlug,
        [FromBody] CreateConvoyageYearRequest req,
        AppDbContext db,
        CancellationToken ct)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Slug == siteSlug, ct);
        if (site is null) return TypedResults.NotFound();

        var exists = await db.ConvoyageYears
            .AnyAsync(y => y.SiteId == site.Id && y.Year == req.Year, ct);
        if (exists)
            return TypedResults.BadRequest($"Já existe um ano {req.Year} para este site.");

        var year = new ConvoyageYear
        {
            SiteId = site.Id,
            Year = req.Year,
            Description = req.Description,
            IsActive = false,
        };
        db.ConvoyageYears.Add(year);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new ConvoyageYearAdminDto(
            year.Id, year.Year, year.IsActive, year.Description, year.CreatedAt, 0));
    }

    private static async Task<Results<Ok, NotFound>> ActivateYear(
        string siteSlug, int id, AppDbContext db, CancellationToken ct)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Slug == siteSlug, ct);
        if (site is null) return TypedResults.NotFound();

        // Desactivar todos, activar só o pedido
        var all = await db.ConvoyageYears
            .Where(y => y.SiteId == site.Id)
            .ToListAsync(ct);

        var target = all.FirstOrDefault(y => y.Id == id);
        if (target is null) return TypedResults.NotFound();

        foreach (var y in all) y.IsActive = (y.Id == id);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<List<ConvoyageInscricaoAdminDto>>, NotFound>> ListInscricoes(
        string siteSlug, int id, AppDbContext db, CancellationToken ct)
    {
        var year = await db.ConvoyageYears
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == id && y.Site.Slug == siteSlug, ct);
        if (year is null) return TypedResults.NotFound();

        var subs = await db.FormSubmissions
            .Where(s => s.ConvoyageYearId == id)
            .OrderBy(s => s.SubmittedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = subs.Select(s =>
        {
            string nome = "", email = "", pais = "", local = "";
            int total = 0;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(s.DataJson);
                var root = doc.RootElement;
                nome  = root.TryGetProperty("nomeCompleto", out var n) ? n.GetString() ?? "" : "";
                email = root.TryGetProperty("email",        out var e) ? e.GetString() ?? "" : "";
                pais  = root.TryGetProperty("pais",         out var p) ? p.GetString() ?? "" : "";
                local = root.TryGetProperty("localRecolha", out var l) ? l.GetString() ?? "" : "";
                total = root.TryGetProperty("totalAves",    out var t) ? t.GetInt32() : 0;
            }
            catch { }
            return new ConvoyageInscricaoAdminDto(
                s.Id, s.SubmittedAt, nome, email, pais, local, total, s.Status.ToString());
        }).ToList();

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<List<ConvoyageCollectionPointDto>>, NotFound>> ListCollectionPoints(
        string siteSlug, int yearId, AppDbContext db, CancellationToken ct)
    {
        var year = await db.ConvoyageYears
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == yearId && y.Site.Slug == siteSlug, ct);
        if (year is null) return TypedResults.NotFound();

        var points = await db.ConvoyageCollectionPoints
            .Where(p => p.ConvoyageYearId == yearId)
            .OrderBy(p => p.SortOrder)
            .AsNoTracking()
            .Select(p => new ConvoyageCollectionPointDto(p.Id, p.Name, p.Location))
            .ToListAsync(ct);

        return TypedResults.Ok(points);
    }

    private static async Task<Results<Ok<ConvoyageCollectionPointDto>, NotFound>> AddCollectionPoint(
        string siteSlug, int yearId,
        [FromBody] AddCollectionPointRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var year = await db.ConvoyageYears
            .FirstOrDefaultAsync(y => y.Id == yearId && y.Site.Slug == siteSlug, ct);
        if (year is null) return TypedResults.NotFound();

        var maxOrder = await db.ConvoyageCollectionPoints
            .Where(p => p.ConvoyageYearId == yearId)
            .MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;

        var point = new ConvoyageCollectionPoint
        {
            ConvoyageYearId = yearId,
            Name = req.Name.Trim(),
            Location = req.Location?.Trim(),
            SortOrder = maxOrder + 1,
        };
        db.ConvoyageCollectionPoints.Add(point);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new ConvoyageCollectionPointDto(point.Id, point.Name, point.Location));
    }

    private static async Task<Results<Ok, NotFound>> DeleteCollectionPoint(
        string siteSlug, int id, AppDbContext db, CancellationToken ct)
    {
        var point = await db.ConvoyageCollectionPoints
            .Include(p => p.ConvoyageYear).ThenInclude(y => y.Site)
            .FirstOrDefaultAsync(p => p.Id == id && p.ConvoyageYear.Site.Slug == siteSlug, ct);
        if (point is null) return TypedResults.NotFound();

        db.ConvoyageCollectionPoints.Remove(point);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok();
    }
}

public record CreateConvoyageYearRequest(int Year, string? Description);
public record AddCollectionPointRequest(string Name, string? Location);

public record ConvoyageYearAdminDto(
    int Id, int Year, bool IsActive, string? Description, DateTime CreatedAt, int TotalInscricoes);

public record ConvoyageInscricaoAdminDto(
    int Id, DateTime SubmittedAt, string Nome, string Email,
    string Pais, string LocalRecolha, int TotalAves, string Status);
