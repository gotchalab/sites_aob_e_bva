using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

/// <summary>
/// CRUD helpers para patrocinadores (banners de parceiros).
/// </summary>
public class SponsorAdminService(AppDbContext db)
{
    public Task<List<Sponsor>> ListAsync(int siteId, string? search = null)
    {
        var q = db.Sponsors.AsNoTracking()
            .Where(s => s.SiteId == siteId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pat = $"%{search.Trim()}%";
            q = q.Where(s => EF.Functions.ILike(s.Name, pat));
        }

        return q.OrderBy(s => s.Tier)
                .ThenBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
    }

    public Task<Sponsor?> GetAsync(int id) =>
        db.Sponsors.FirstOrDefaultAsync(s => s.Id == id);

    public async Task<Sponsor> SaveAsync(Sponsor s)
    {
        if (string.IsNullOrWhiteSpace(s.Slug))
            s.Slug = Slugify(s.Name);

        s.UpdatedAt = DateTime.UtcNow;

        if (s.Id == 0)
        {
            s.CreatedAt = DateTime.UtcNow;
            db.Sponsors.Add(s);
        }
        else
        {
            db.Sponsors.Update(s);
        }
        await db.SaveChangesAsync();
        return s;
    }

    public async Task DeleteAsync(int id)
    {
        var s = await db.Sponsors.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return;
        db.Sponsors.Remove(s);
        await db.SaveChangesAsync();
    }

    private static string Slugify(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var normalized = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
