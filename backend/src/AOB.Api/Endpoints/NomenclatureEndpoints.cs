using AOB.Application.Contracts;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AOB.Api.Endpoints;

public static class NomenclatureEndpoints
{
    public static IEndpointRouteBuilder MapNomenclature(this IEndpointRouteBuilder app)
    {
        // ── Public ──────────────────────────────────────────────────────────
        app.MapGet("/api/sites/{siteSlug}/convoyage/nomenclature/active", GetActive)
            .WithTags("Nomenclature");
        app.MapGet("/api/sites/{siteSlug}/convoyage/nomenclature/{year:int}", GetByYear)
            .WithTags("Nomenclature");

        // ── Admin ───────────────────────────────────────────────────────────
        var admin = app.MapGroup("/api/admin/sites/{siteSlug}/nomenclature")
            .WithTags("Nomenclature Admin")
            .RequireAuthorization("Admin");

        admin.MapGet("/years/{yearId:int}/groups", ListGroups);
        admin.MapPost("/years/{yearId:int}/groups", CreateGroup);
        admin.MapPut("/groups/{groupId:int}", UpdateGroup);
        admin.MapDelete("/groups/{groupId:int}", DeleteGroup);

        admin.MapGet("/groups/{groupId:int}/classes", ListClasses);
        admin.MapPost("/groups/{groupId:int}/classes", CreateClass);
        admin.MapPut("/classes/{classId:int}", UpdateClass);
        admin.MapDelete("/classes/{classId:int}", DeleteClass);

        admin.MapPost("/years/{yearId:int}/clone", CloneFromYear);

        return app;
    }

    // ── Public handlers ─────────────────────────────────────────────────────

    private static Task<Results<Ok<NomenclatureVersionDto>, NotFound>> GetActive(
        string siteSlug, AppDbContext db, CancellationToken ct)
        => LoadVersion(db, ct, y => y.Site.Slug == siteSlug && y.IsActive);

    private static Task<Results<Ok<NomenclatureVersionDto>, NotFound>> GetByYear(
        string siteSlug, int year, AppDbContext db, CancellationToken ct)
        => LoadVersion(db, ct, y => y.Site.Slug == siteSlug && y.Year == year);

    private static async Task<Results<Ok<NomenclatureVersionDto>, NotFound>> LoadVersion(
        AppDbContext db, CancellationToken ct,
        System.Linq.Expressions.Expression<Func<ConvoyageYear, bool>> predicate)
    {
        var year = await db.ConvoyageYears
            .AsNoTracking()
            .Where(predicate)
            .Select(y => new { y.Id, y.Year })
            .FirstOrDefaultAsync(ct);
        if (year is null) return TypedResults.NotFound();

        var groups = await db.NomenclatureGroups
            .AsNoTracking()
            .Where(g => g.ConvoyageYearId == year.Id)
            .OrderBy(g => g.SortOrder)
            .Select(g => new NomenclatureGroupDto(
                g.Id, g.CodePrefix, g.DisplayName, g.Species, g.EntryType,
                g.Classes
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new NomenclatureClassDto(c.Id, c.Code, c.Mutation, c.Notes))
                    .ToList()))
            .ToListAsync(ct);

        return TypedResults.Ok(new NomenclatureVersionDto(year.Id, year.Year, groups));
    }

    // ── Admin handlers ──────────────────────────────────────────────────────

    private static async Task<Results<Ok<List<NomenclatureGroupAdminDto>>, NotFound>> ListGroups(
        string siteSlug, int yearId, AppDbContext db, CancellationToken ct)
    {
        var ownsYear = await db.ConvoyageYears
            .AnyAsync(y => y.Id == yearId && y.Site.Slug == siteSlug, ct);
        if (!ownsYear) return TypedResults.NotFound();

        var groups = await db.NomenclatureGroups
            .AsNoTracking()
            .Where(g => g.ConvoyageYearId == yearId)
            .OrderBy(g => g.SortOrder)
            .Select(g => new NomenclatureGroupAdminDto(
                g.Id, g.CodePrefix, g.DisplayName, g.Species, g.EntryType,
                g.SortOrder, g.Classes.Count))
            .ToListAsync(ct);

        return TypedResults.Ok(groups);
    }

    private static async Task<Results<Ok<NomenclatureGroupAdminDto>, BadRequest<string>, NotFound>> CreateGroup(
        string siteSlug, int yearId, [FromBody] CreateGroupRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var year = await db.ConvoyageYears
            .FirstOrDefaultAsync(y => y.Id == yearId && y.Site.Slug == siteSlug, ct);
        if (year is null) return TypedResults.NotFound();

        var prefix = (req.CodePrefix ?? "").Trim();
        if (prefix.Length != 3)
            return TypedResults.BadRequest("CodePrefix deve ter 3 caracteres.");

        var exists = await db.NomenclatureGroups
            .AnyAsync(g => g.ConvoyageYearId == yearId && g.CodePrefix == prefix, ct);
        if (exists)
            return TypedResults.BadRequest($"Já existe um grupo {prefix} neste ano.");

        var g = new NomenclatureGroup
        {
            ConvoyageYearId = yearId,
            CodePrefix = prefix,
            DisplayName = req.DisplayName.Trim(),
            Species = req.Species,
            EntryType = req.EntryType,
            SortOrder = req.SortOrder,
        };
        db.NomenclatureGroups.Add(g);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new NomenclatureGroupAdminDto(
            g.Id, g.CodePrefix, g.DisplayName, g.Species, g.EntryType, g.SortOrder, 0));
    }

    private static async Task<Results<Ok, NotFound>> UpdateGroup(
        string siteSlug, int groupId, [FromBody] UpdateGroupRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var g = await db.NomenclatureGroups
            .Include(x => x.ConvoyageYear).ThenInclude(y => y.Site)
            .FirstOrDefaultAsync(x => x.Id == groupId && x.ConvoyageYear.Site.Slug == siteSlug, ct);
        if (g is null) return TypedResults.NotFound();

        g.DisplayName = req.DisplayName.Trim();
        g.SortOrder = req.SortOrder;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<string>, NotFound>> DeleteGroup(
        string siteSlug, int groupId, AppDbContext db, CancellationToken ct)
    {
        var g = await db.NomenclatureGroups
            .Include(x => x.ConvoyageYear).ThenInclude(y => y.Site)
            .Include(x => x.Classes).ThenInclude(c => c.BirdEntries)
            .FirstOrDefaultAsync(x => x.Id == groupId && x.ConvoyageYear.Site.Slug == siteSlug, ct);
        if (g is null) return TypedResults.NotFound();

        if (g.Classes.Any(c => c.BirdEntries.Any()))
            return TypedResults.BadRequest("Não é possível apagar: existem inscrições associadas.");

        db.NomenclatureGroups.Remove(g);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<List<ClassAdminDto>>, NotFound>> ListClasses(
        string siteSlug, int groupId, AppDbContext db, CancellationToken ct)
    {
        var ownsGroup = await db.NomenclatureGroups
            .AnyAsync(g => g.Id == groupId && g.ConvoyageYear.Site.Slug == siteSlug, ct);
        if (!ownsGroup) return TypedResults.NotFound();

        var classes = await db.NomenclatureClasses
            .AsNoTracking()
            .Where(c => c.NomenclatureGroupId == groupId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ClassAdminDto(c.Id, c.Code, c.Mutation, c.SortOrder, c.IsActive, c.Notes))
            .ToListAsync(ct);

        return TypedResults.Ok(classes);
    }

    private static async Task<Results<Ok<ClassAdminDto>, BadRequest<string>, NotFound>> CreateClass(
        string siteSlug, int groupId, [FromBody] CreateClassRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var g = await db.NomenclatureGroups
            .Include(x => x.ConvoyageYear).ThenInclude(y => y.Site)
            .FirstOrDefaultAsync(x => x.Id == groupId && x.ConvoyageYear.Site.Slug == siteSlug, ct);
        if (g is null) return TypedResults.NotFound();

        var code = (req.Code ?? "").Trim();
        var mutation = (req.Mutation ?? "").Trim();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(mutation))
            return TypedResults.BadRequest("Code e Mutation são obrigatórios.");
        if (!code.StartsWith(g.CodePrefix + "/"))
            return TypedResults.BadRequest($"Code deve começar por '{g.CodePrefix}/'.");

        var exists = await db.NomenclatureClasses
            .AnyAsync(c => c.NomenclatureGroupId == groupId && c.Code == code && c.Mutation == mutation, ct);
        if (exists)
            return TypedResults.BadRequest("Já existe uma classe com este Code+Mutation.");

        var c = new NomenclatureClass
        {
            NomenclatureGroupId = groupId,
            Code = code,
            Mutation = mutation,
            SortOrder = req.SortOrder,
            IsActive = true,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
        };
        db.NomenclatureClasses.Add(c);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new ClassAdminDto(c.Id, c.Code, c.Mutation, c.SortOrder, c.IsActive, c.Notes));
    }

    private static async Task<Results<Ok, BadRequest<string>, NotFound>> UpdateClass(
        string siteSlug, int classId, [FromBody] UpdateClassRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var c = await db.NomenclatureClasses
            .Include(x => x.NomenclatureGroup).ThenInclude(g => g.ConvoyageYear).ThenInclude(y => y.Site)
            .FirstOrDefaultAsync(x => x.Id == classId && x.NomenclatureGroup.ConvoyageYear.Site.Slug == siteSlug, ct);
        if (c is null) return TypedResults.NotFound();

        var code = (req.Code ?? "").Trim();
        var mutation = (req.Mutation ?? "").Trim();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(mutation))
            return TypedResults.BadRequest("Code e Mutation são obrigatórios.");
        if (!code.StartsWith(c.NomenclatureGroup.CodePrefix + "/"))
            return TypedResults.BadRequest($"Code deve começar por '{c.NomenclatureGroup.CodePrefix}/'.");

        c.Code = code;
        c.Mutation = mutation;
        c.SortOrder = req.SortOrder;
        c.IsActive = req.IsActive;
        c.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<string>, NotFound>> DeleteClass(
        string siteSlug, int classId, AppDbContext db, CancellationToken ct)
    {
        var c = await db.NomenclatureClasses
            .Include(x => x.NomenclatureGroup).ThenInclude(g => g.ConvoyageYear).ThenInclude(y => y.Site)
            .Include(x => x.BirdEntries)
            .FirstOrDefaultAsync(x => x.Id == classId && x.NomenclatureGroup.ConvoyageYear.Site.Slug == siteSlug, ct);
        if (c is null) return TypedResults.NotFound();

        if (c.BirdEntries.Any())
            return TypedResults.BadRequest("Não é possível apagar: classe usada em inscrições.");

        db.NomenclatureClasses.Remove(c);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok();
    }

    // ── Clone whole nomenclature from another year ──────────────────────────

    private static async Task<Results<Ok<int>, BadRequest<string>, NotFound>> CloneFromYear(
        string siteSlug, int yearId, [FromBody] CloneNomenclatureRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var target = await db.ConvoyageYears
            .FirstOrDefaultAsync(y => y.Id == yearId && y.Site.Slug == siteSlug, ct);
        if (target is null) return TypedResults.NotFound();

        var source = await db.ConvoyageYears
            .FirstOrDefaultAsync(y => y.Id == req.SourceConvoyageYearId && y.Site.Slug == siteSlug, ct);
        if (source is null) return TypedResults.NotFound();

        var already = await db.NomenclatureGroups.AnyAsync(g => g.ConvoyageYearId == yearId, ct);
        if (already)
            return TypedResults.BadRequest("O ano destino já tem nomenclatura. Apaga-a antes de clonar.");

        var sourceGroups = await db.NomenclatureGroups
            .AsNoTracking()
            .Include(g => g.Classes)
            .Where(g => g.ConvoyageYearId == source.Id)
            .ToListAsync(ct);

        var copied = 0;
        foreach (var g in sourceGroups)
        {
            var newGroup = new NomenclatureGroup
            {
                ConvoyageYearId = yearId,
                CodePrefix = g.CodePrefix,
                DisplayName = g.DisplayName,
                Species = g.Species,
                EntryType = g.EntryType,
                SortOrder = g.SortOrder,
                Classes = g.Classes.Select(c => new NomenclatureClass
                {
                    Code = c.Code,
                    Mutation = c.Mutation,
                    SortOrder = c.SortOrder,
                    IsActive = c.IsActive,
                    Notes = c.Notes,
                }).ToList(),
            };
            db.NomenclatureGroups.Add(newGroup);
            copied += newGroup.Classes.Count;
        }
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(copied);
    }
}
