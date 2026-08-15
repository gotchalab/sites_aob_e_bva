using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public class NomenclatureAdminService(AppDbContext db)
{
    public record GroupSummary(
        int Id, string CodePrefix, string DisplayName,
        SpeciesCode Species, EntryTypeCode EntryType,
        int SortOrder, int ClassCount);

    public record ClassRow(
        int Id, string Code, string Mutation, int SortOrder,
        bool IsActive, string? Notes);

    // ── Group operations ────────────────────────────────────────────────────

    public Task<List<GroupSummary>> ListGroupsAsync(int convoyageYearId) =>
        db.NomenclatureGroups
            .Where(g => g.ConvoyageYearId == convoyageYearId)
            .OrderBy(g => g.SortOrder)
            .Select(g => new GroupSummary(
                g.Id, g.CodePrefix, g.DisplayName, g.Species, g.EntryType,
                g.SortOrder, g.Classes.Count))
            .AsNoTracking()
            .ToListAsync();

    public async Task<(NomenclatureGroup? Group, string? Error)> CreateGroupAsync(
        int convoyageYearId, string codePrefix, string displayName,
        SpeciesCode species, EntryTypeCode entryType, int sortOrder)
    {
        codePrefix = (codePrefix ?? "").Trim();
        if (codePrefix.Length != 3)
            return (null, "CodePrefix deve ter 3 caracteres.");

        var exists = await db.NomenclatureGroups
            .AnyAsync(g => g.ConvoyageYearId == convoyageYearId && g.CodePrefix == codePrefix);
        if (exists)
            return (null, $"Já existe um grupo {codePrefix} neste ano.");

        var g = new NomenclatureGroup
        {
            ConvoyageYearId = convoyageYearId,
            CodePrefix = codePrefix,
            DisplayName = displayName.Trim(),
            Species = species,
            EntryType = entryType,
            SortOrder = sortOrder,
        };
        db.NomenclatureGroups.Add(g);
        await db.SaveChangesAsync();
        return (g, null);
    }

    public async Task<bool> UpdateGroupAsync(int groupId, string displayName, int sortOrder)
    {
        var g = await db.NomenclatureGroups.FirstOrDefaultAsync(x => x.Id == groupId);
        if (g is null) return false;
        g.DisplayName = displayName.Trim();
        g.SortOrder = sortOrder;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Ok, string? Error)> DeleteGroupAsync(int groupId)
    {
        var g = await db.NomenclatureGroups
            .Include(x => x.Classes).ThenInclude(c => c.BirdEntries)
            .FirstOrDefaultAsync(x => x.Id == groupId);
        if (g is null) return (false, "Grupo não encontrado.");
        if (g.Classes.Any(c => c.BirdEntries.Any()))
            return (false, "Não é possível apagar: existem inscrições associadas.");
        db.NomenclatureGroups.Remove(g);
        await db.SaveChangesAsync();
        return (true, null);
    }

    // ── Class operations ────────────────────────────────────────────────────

    public Task<List<ClassRow>> ListClassesAsync(int groupId) =>
        db.NomenclatureClasses
            .Where(c => c.NomenclatureGroupId == groupId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ClassRow(c.Id, c.Code, c.Mutation, c.SortOrder, c.IsActive, c.Notes))
            .AsNoTracking()
            .ToListAsync();

    public async Task<(NomenclatureClass? Class, string? Error)> CreateClassAsync(
        int groupId, string code, string mutation, int sortOrder, string? notes)
    {
        var g = await db.NomenclatureGroups.FirstOrDefaultAsync(x => x.Id == groupId);
        if (g is null) return (null, "Grupo não encontrado.");
        code = (code ?? "").Trim();
        mutation = (mutation ?? "").Trim();
        if (code.Length == 0 || mutation.Length == 0) return (null, "Code e Mutation são obrigatórios.");
        if (!code.StartsWith(g.CodePrefix + "/"))
            return (null, $"Code deve começar por '{g.CodePrefix}/'.");

        var exists = await db.NomenclatureClasses
            .AnyAsync(c => c.NomenclatureGroupId == groupId && c.Code == code && c.Mutation == mutation);
        if (exists) return (null, "Já existe uma classe com este Code+Mutation.");

        var c = new NomenclatureClass
        {
            NomenclatureGroupId = groupId,
            Code = code, Mutation = mutation, SortOrder = sortOrder, IsActive = true,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        db.NomenclatureClasses.Add(c);
        await db.SaveChangesAsync();
        return (c, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateClassAsync(
        int classId, string code, string mutation, int sortOrder, bool isActive, string? notes)
    {
        var c = await db.NomenclatureClasses
            .Include(x => x.NomenclatureGroup)
            .FirstOrDefaultAsync(x => x.Id == classId);
        if (c is null) return (false, "Classe não encontrada.");
        code = (code ?? "").Trim();
        mutation = (mutation ?? "").Trim();
        if (code.Length == 0 || mutation.Length == 0) return (false, "Code e Mutation são obrigatórios.");
        if (!code.StartsWith(c.NomenclatureGroup.CodePrefix + "/"))
            return (false, $"Code deve começar por '{c.NomenclatureGroup.CodePrefix}/'.");
        c.Code = code;
        c.Mutation = mutation;
        c.SortOrder = sortOrder;
        c.IsActive = isActive;
        c.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteClassAsync(int classId)
    {
        var c = await db.NomenclatureClasses
            .Include(x => x.BirdEntries)
            .FirstOrDefaultAsync(x => x.Id == classId);
        if (c is null) return (false, "Classe não encontrada.");
        if (c.BirdEntries.Any())
            return (false, "Não é possível apagar: classe usada em inscrições.");
        db.NomenclatureClasses.Remove(c);
        await db.SaveChangesAsync();
        return (true, null);
    }

    // ── Clone whole nomenclature ────────────────────────────────────────────

    public async Task<(int Copied, string? Error)> CloneAsync(int targetYearId, int sourceYearId)
    {
        var already = await db.NomenclatureGroups.AnyAsync(g => g.ConvoyageYearId == targetYearId);
        if (already)
            return (0, "O ano destino já tem nomenclatura. Apaga-a antes de clonar.");

        var source = await db.NomenclatureGroups
            .AsNoTracking()
            .Include(g => g.Classes)
            .Where(g => g.ConvoyageYearId == sourceYearId)
            .ToListAsync();

        var copied = 0;
        foreach (var g in source)
        {
            var newGroup = new NomenclatureGroup
            {
                ConvoyageYearId = targetYearId,
                CodePrefix = g.CodePrefix,
                DisplayName = g.DisplayName,
                Species = g.Species,
                EntryType = g.EntryType,
                SortOrder = g.SortOrder,
                Classes = g.Classes.Select(c => new NomenclatureClass
                {
                    Code = c.Code, Mutation = c.Mutation, SortOrder = c.SortOrder,
                    IsActive = c.IsActive, Notes = c.Notes,
                }).ToList(),
            };
            db.NomenclatureGroups.Add(newGroup);
            copied += newGroup.Classes.Count;
        }
        await db.SaveChangesAsync();
        return (copied, null);
    }
}
