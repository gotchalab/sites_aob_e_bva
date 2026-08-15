using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public class ConvoyageAdminService(AppDbContext db)
{
    public record YearWithPoints(
        int Id, int Year, bool IsActive, string? Description, DateTime CreatedAt,
        int TotalInscricoes, List<ConvoyageCollectionPoint> CollectionPoints,
        int NumCargasAlvo, int CapacidadePorCarga, int MinPorCarga,
        string TransportadorasJson,
        DateTime? RegistrationClosesAt,
        PricingConfig Pricing);

    public record PricingConfig(
        decimal PrecoInscricao, decimal PrecoAveBva, decimal PrecoGaiola,
        decimal TarifaTransporteSocio, decimal TarifaTransporteNaoSocio,
        decimal TarifaAdquirenteSocio, decimal TarifaAdquirenteNaoSocio,
        decimal Quota);

    public async Task<List<YearWithPoints>> ListYearsAsync(int siteId)
    {
        var years = await db.ConvoyageYears
            .Where(y => y.SiteId == siteId)
            .OrderByDescending(y => y.Year)
            .Select(y => new
            {
                y.Id, y.Year, y.IsActive, y.Description, y.CreatedAt,
                TotalInscricoes = y.Submissions.Count(s => s.FormType == FormType.InscricaoConvoyage),
                Points = y.CollectionPoints.OrderBy(p => p.SortOrder).ToList(),
                y.NumCargasAlvo, y.CapacidadePorCarga, y.MinPorCarga, y.TransportadorasJson,
                y.RegistrationClosesAt,
                y.PrecoInscricao, y.PrecoAveBva, y.PrecoGaiola,
                y.TarifaTransporteSocio, y.TarifaTransporteNaoSocio,
                y.TarifaAdquirenteSocio, y.TarifaAdquirenteNaoSocio,
                y.Quota,
            })
            .AsNoTracking()
            .ToListAsync();

        return years.Select(y => new YearWithPoints(
            y.Id, y.Year, y.IsActive, y.Description, y.CreatedAt,
            y.TotalInscricoes, y.Points,
            y.NumCargasAlvo, y.CapacidadePorCarga, y.MinPorCarga, y.TransportadorasJson ?? "{}",
            y.RegistrationClosesAt,
            new PricingConfig(
                y.PrecoInscricao, y.PrecoAveBva, y.PrecoGaiola,
                y.TarifaTransporteSocio, y.TarifaTransporteNaoSocio,
                y.TarifaAdquirenteSocio, y.TarifaAdquirenteNaoSocio,
                y.Quota))).ToList();
    }

    public async Task<string?> UpdatePricingAsync(int yearId, PricingConfig p)
    {
        if (p.PrecoInscricao < 0 || p.PrecoAveBva < 0 || p.PrecoGaiola < 0
            || p.TarifaTransporteSocio < 0 || p.TarifaTransporteNaoSocio < 0
            || p.TarifaAdquirenteSocio < 0 || p.TarifaAdquirenteNaoSocio < 0
            || p.Quota < 0)
            return "Todos os valores têm de ser >= 0.";
        var y = await db.ConvoyageYears.FirstOrDefaultAsync(x => x.Id == yearId);
        if (y is null) return "Ano não encontrado.";
        y.PrecoInscricao = p.PrecoInscricao;
        y.PrecoAveBva = p.PrecoAveBva;
        y.PrecoGaiola = p.PrecoGaiola;
        y.TarifaTransporteSocio = p.TarifaTransporteSocio;
        y.TarifaTransporteNaoSocio = p.TarifaTransporteNaoSocio;
        y.TarifaAdquirenteSocio = p.TarifaAdquirenteSocio;
        y.TarifaAdquirenteNaoSocio = p.TarifaAdquirenteNaoSocio;
        y.Quota = p.Quota;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Actualiza (ou limpa, se <paramref name="closesAtUtc"/> for null) a data
    /// de fecho das inscrições do ano indicado.
    /// </summary>
    public async Task<string?> UpdateRegistrationClosesAsync(int yearId, DateTime? closesAtUtc)
    {
        var y = await db.ConvoyageYears.FirstOrDefaultAsync(x => x.Id == yearId);
        if (y is null) return "Ano não encontrado.";
        y.RegistrationClosesAt = closesAtUtc;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> ReorderCollectionPointAsync(int pointId, int direction)
    {
        var point = await db.ConvoyageCollectionPoints.FirstOrDefaultAsync(p => p.Id == pointId);
        if (point is null) return "Ponto não encontrado.";

        var siblings = await db.ConvoyageCollectionPoints
            .Where(p => p.ConvoyageYearId == point.ConvoyageYearId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        var idx = siblings.FindIndex(p => p.Id == pointId);
        var swapIdx = idx + direction;
        if (idx < 0 || swapIdx < 0 || swapIdx >= siblings.Count) return null;

        (siblings[idx].SortOrder, siblings[swapIdx].SortOrder) =
            (siblings[swapIdx].SortOrder, siblings[idx].SortOrder);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<(ConvoyageYear? Year, string? Error)> CreateYearAsync(
        int siteId, int year, string? description)
    {
        var exists = await db.ConvoyageYears
            .AnyAsync(y => y.SiteId == siteId && y.Year == year);
        if (exists)
            return (null, $"Já existe um ano {year} para este site.");

        var y = new ConvoyageYear
        {
            SiteId = siteId,
            Year = year,
            Description = description?.Trim(),
            IsActive = false,
        };
        db.ConvoyageYears.Add(y);
        await db.SaveChangesAsync();
        return (y, null);
    }

    public async Task ActivateYearAsync(int siteId, int yearId)
    {
        var all = await db.ConvoyageYears
            .Where(y => y.SiteId == siteId)
            .ToListAsync();
        foreach (var y in all) y.IsActive = y.Id == yearId;
        await db.SaveChangesAsync();
    }

    public async Task AddCollectionPointAsync(int yearId, string name, string? location)
    {
        var maxOrder = await db.ConvoyageCollectionPoints
            .Where(p => p.ConvoyageYearId == yearId)
            .MaxAsync(p => (int?)p.SortOrder) ?? 0;

        db.ConvoyageCollectionPoints.Add(new ConvoyageCollectionPoint
        {
            ConvoyageYearId = yearId,
            Name = name.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            SortOrder = maxOrder + 1,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteCollectionPointAsync(int id)
    {
        var p = await db.ConvoyageCollectionPoints.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return;
        db.ConvoyageCollectionPoints.Remove(p);
        await db.SaveChangesAsync();
    }

    public async Task<string?> UpdateCollectionPointAsync(int id, string name, string? location)
    {
        if (string.IsNullOrWhiteSpace(name)) return "O nome do ponto é obrigatório.";
        var p = await db.ConvoyageCollectionPoints.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return "Ponto de recolha não encontrado.";
        p.Name = name.Trim();
        p.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteYearAsync(int yearId)
    {
        var year = await db.ConvoyageYears.FirstOrDefaultAsync(y => y.Id == yearId);
        if (year is null) return "Ano não encontrado.";
        if (year.IsActive) return "Não é possível eliminar o ano ativo. Ative outro ano primeiro.";

        var hasSubmissions = await db.FormSubmissions.AnyAsync(s => s.ConvoyageYearId == yearId);
        if (hasSubmissions)
            return "Não é possível eliminar: existem inscrições associadas a este ano.";

        db.ConvoyageYears.Remove(year);
        await db.SaveChangesAsync();
        return null;
    }
}
