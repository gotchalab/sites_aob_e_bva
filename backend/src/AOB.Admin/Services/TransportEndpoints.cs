using System.Text.RegularExpressions;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public static class TransportEndpoints
{
    public static WebApplication MapTransportEndpoints(this WebApplication app)
    {
        app.MapGet("/convoyage/{yearId:int}/transportes/export", async (
            int yearId, TransportPlanAdminService svc, AppDbContext db) =>
        {
            var bytes = await svc.ExportXlsxAsync(yearId);
            if (bytes is null) return Results.NotFound();

            var year = await db.ConvoyageYears.AsNoTracking()
                .Include(y => y.Site)
                .FirstOrDefaultAsync(y => y.Id == yearId);

            var name = BuildFileName(year, yearId);
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                name);
        }).RequireAuthorization();

        return app;
    }

    private static string BuildFileName(ConvoyageYear? year, int fallbackId)
    {
        if (year is null) return $"convoyage-{fallbackId}-transportes.xlsx";

        var parts = new List<string> { "convoyage" };
        var siteSlug = Slugify(year.Site?.Slug ?? year.Site?.Name);
        if (!string.IsNullOrEmpty(siteSlug)) parts.Add(siteSlug);
        parts.Add(year.Year.ToString());
        var desc = Slugify(year.Description);
        if (!string.IsNullOrEmpty(desc)) parts.Add(desc);
        parts.Add("transportes");
        return string.Join("-", parts) + ".xlsx";
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        var ascii = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        ascii = Regex.Replace(ascii, "[^a-z0-9]+", "-").Trim('-');
        return ascii;
    }
}
