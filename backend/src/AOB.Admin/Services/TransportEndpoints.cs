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

            var name = BuildFileName(year, yearId, "transportes", "xlsx");
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                name);
        }).RequireAuthorization();

        // Etiquetas Avery 3421 (33 por folha, 70×25.4 mm) — por ponto de recolha.
        // Enviamos Content-Disposition: attachment para forçar download em vez
        // de o browser abrir o PDF numa tab nova.
        app.MapGet("/convoyage/{yearId:int}/pontos/{pointId:int}/etiquetas.pdf", async (
            int yearId, int pointId, TransportPlanAdminService svc, AppDbContext db, HttpContext http) =>
        {
            var res = await svc.ExportEtiquetasPorPontoAsync(yearId, pointId);
            if (res is null) return Results.NotFound();

            var year = await db.ConvoyageYears.AsNoTracking()
                .Include(y => y.Site)
                .FirstOrDefaultAsync(y => y.Id == yearId);

            var suffix = string.IsNullOrEmpty(res.ScopeSlug)
                ? "avery3421"
                : $"{res.ScopeSlug}-avery3421";
            var name = BuildFileName(year, yearId, $"etiquetas-{suffix}", "pdf");
            http.Response.Headers.ContentDisposition = $"attachment; filename=\"{name}\"";
            return Results.File(res.Bytes, "application/pdf", name);
        }).RequireAuthorization();

        // Etiquetas Avery 3421 por inscrição individual.
        app.MapGet("/convoyage/{yearId:int}/inscricoes/{submissionId:int}/etiquetas.pdf", async (
            int yearId, int submissionId, TransportPlanAdminService svc, AppDbContext db, HttpContext http) =>
        {
            var res = await svc.ExportEtiquetasPorInscricaoAsync(yearId, submissionId);
            if (res is null) return Results.NotFound();

            var year = await db.ConvoyageYears.AsNoTracking()
                .Include(y => y.Site)
                .FirstOrDefaultAsync(y => y.Id == yearId);

            var suffix = string.IsNullOrEmpty(res.ScopeSlug)
                ? $"insc-{submissionId}-avery3421"
                : $"{res.ScopeSlug}-avery3421";
            var name = BuildFileName(year, yearId, $"etiquetas-{suffix}", "pdf");
            http.Response.Headers.ContentDisposition = $"attachment; filename=\"{name}\"";
            return Results.File(res.Bytes, "application/pdf", name);
        }).RequireAuthorization();

        return app;
    }

    private static string BuildFileName(ConvoyageYear? year, int fallbackId, string kind, string ext)
    {
        if (year is null) return $"convoyage-{fallbackId}-{kind}.{ext}";

        var parts = new List<string> { "convoyage" };
        var siteSlug = Slugify(year.Site?.Slug ?? year.Site?.Name);
        if (!string.IsNullOrEmpty(siteSlug)) parts.Add(siteSlug);
        parts.Add(year.Year.ToString());
        var desc = Slugify(year.Description);
        if (!string.IsNullOrEmpty(desc)) parts.Add(desc);
        parts.Add(kind);
        return string.Join("-", parts) + "." + ext;
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
