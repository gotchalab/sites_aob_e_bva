using System.IO.Compression;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace AOB.Admin.Services;

public static class AuthEndpoints
{
    private const long MaxUploadBytes = 50L * 1024 * 1024;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] string? returnUrl,
            SignInManager<ApplicationUser> signIn,
            HttpContext http) =>
        {
            var result = await signIn.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var target = string.IsNullOrWhiteSpace(returnUrl) || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
                    ? "/"
                    : returnUrl;
                return Results.Redirect(target);
            }
            var err = result.IsLockedOut ? "locked" : "invalid";
            var back = $"/login?error={err}" + (string.IsNullOrWhiteSpace(returnUrl) ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}");
            return Results.Redirect(back);
        }).DisableAntiforgery();

        app.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Redirect("/login");
        }).DisableAntiforgery();

        app.MapPost("/admin/upload-inline", async (
            HttpRequest req,
            UploadService uploads) =>
        {
            if (!IsSameOrigin(req)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!req.HasFormContentType) return Results.BadRequest();
            var form = await req.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0) return Results.BadRequest();
            if (file.Length > MaxUploadBytes) return Results.BadRequest(new { error = "Ficheiro excede 50 MB" });

            var siteSlug = form["site"].ToString();
            if (string.IsNullOrWhiteSpace(siteSlug)) siteSlug = "aob";

            var kindRaw = form["kind"].ToString();
            if (string.IsNullOrWhiteSpace(kindRaw) && req.Query.TryGetValue("kind", out var qk)) kindRaw = qk.ToString();
            var kind = kindRaw == "downloads" ? "downloads" : "images";

            await using var stream = file.OpenReadStream();
            var res = await uploads.SaveAsync(stream, file.FileName, siteSlug, kind);
            return Results.Json(new
            {
                url = res.PublicPath,
                name = res.FileName,
                size = res.FileSize,
                mime = res.MimeType
            });
        })
        .DisableAntiforgery()
        .RequireAuthorization();

        app.MapGet("/admin/downloads-lookup", async (
            string? site,
            string? q,
            int? limit,
            AppDbContext db) =>
        {
            var siteSlug = string.IsNullOrWhiteSpace(site) ? "aob" : site;
            var take = Math.Clamp(limit ?? 50, 1, 200);
            var siteId = await db.Sites.AsNoTracking()
                .Where(s => s.Slug == siteSlug)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
            if (siteId is null) return Results.Json(Array.Empty<object>());

            var query = db.Downloads.AsNoTracking().Where(d => d.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(d => EF.Functions.ILike(d.Title, like) || EF.Functions.ILike(d.FileName, like));
            }

            var rows = await query
                .OrderByDescending(d => d.UpdatedAt)
                .Take(take)
                .Select(d => new
                {
                    id = d.Id,
                    title = d.Title,
                    fileName = d.FileName,
                    size = d.FileSize,
                    mime = d.MimeType,
                    url = d.StoragePath,
                    published = d.IsPublished
                })
                .ToListAsync();
            return Results.Json(rows);
        }).RequireAuthorization();

        app.MapPost("/admin/downloads-quick-create", async (
            HttpRequest req,
            UploadService uploads,
            AppDbContext db) =>
        {
            if (!IsSameOrigin(req)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!req.HasFormContentType) return Results.BadRequest();
            var form = await req.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0) return Results.BadRequest();
            if (file.Length > MaxUploadBytes) return Results.BadRequest(new { error = "Ficheiro excede 50 MB" });

            var siteSlug = form["site"].ToString();
            if (string.IsNullOrWhiteSpace(siteSlug)) siteSlug = "aob";
            var title = form["title"].ToString();
            if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileNameWithoutExtension(file.FileName);

            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == siteSlug);
            if (site is null) return Results.BadRequest(new { error = "site invalido" });

            await using var stream = file.OpenReadStream();
            var res = await uploads.SaveAsync(stream, file.FileName, siteSlug, "downloads");

            var slug = await MakeUniqueSlug(db, site.Id, SlugFrom(title));
            var dl = new Download
            {
                SiteId = site.Id,
                Title = title,
                Slug = slug,
                FileName = res.FileName,
                StoragePath = res.PublicPath,
                FileSize = res.FileSize,
                MimeType = res.MimeType,
                IsPublished = true,
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Downloads.Add(dl);
            await db.SaveChangesAsync();

            return Results.Json(new
            {
                id = dl.Id,
                title = dl.Title,
                fileName = dl.FileName,
                size = dl.FileSize,
                mime = dl.MimeType,
                url = dl.StoragePath
            });
        })
        .DisableAntiforgery()
        .RequireAuthorization();

        app.MapGet("/formularios/{id:int}/assinatura", async (
            int id,
            AppDbContext db,
            IConfiguration config,
            IHostEnvironment env,
            HttpContext http,
            CancellationToken ct) =>
        {
            var form = await db.FormSubmissions.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id, ct);
            if (form is null || form.FormType != FormType.InscricaoConvoyage) return Results.NotFound();

            string? rel = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(form.DataJson);
                if (doc.RootElement.TryGetProperty("AssinaturaPath", out var v) &&
                    v.ValueKind == System.Text.Json.JsonValueKind.String)
                    rel = v.GetString();
            }
            catch { }
            if (string.IsNullOrWhiteSpace(rel)) return Results.NotFound();

            var storageRoot = config["Inscricoes:StorageRoot"];
            if (string.IsNullOrWhiteSpace(storageRoot))
                storageRoot = Path.Combine(env.ContentRootPath, "PrivateData", "inscricoes");
            var rootFull = Path.GetFullPath(storageRoot);
            var abs = Path.GetFullPath(Path.Combine(rootFull, rel));
            if (!abs.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) return Results.Forbid();
            if (!File.Exists(abs)) return Results.NotFound();

            var bytes = await File.ReadAllBytesAsync(abs, ct);
            http.Response.ContentType = "image/png";
            http.Response.Headers.CacheControl = "no-store, must-revalidate";
            await http.Response.Body.WriteAsync(bytes, ct);
            return Results.Empty;
        }).RequireAuthorization();

        app.MapGet("/formularios/{id:int}/traces", async (
            int id,
            AppDbContext db,
            IConfiguration config,
            IHostEnvironment env,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await AOB.Application.Forms.TracesPdfBuilder.BuildAsync(db, env, config, id, null, ct);
            if (result is null) return Results.NotFound();
            http.Response.Headers.ContentDisposition = ContentDispositionHeader("inline", result.FileName);
            http.Response.ContentType = "application/pdf";
            http.Response.Headers.CacheControl = "no-store, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            await http.Response.Body.WriteAsync(result.Bytes, ct);
            return Results.Empty;
        }).RequireAuthorization();

        // Bulk: PDF único com todas as Declarações TRACES do ano (opcionalmente
        // filtradas por estado). Regenera cada PDF via TracesPdfBuilder para
        // reflectir os dados actuais do ano + assinaturas persistidas, e depois
        // funde-os num único documento pela ordem dos IDs.
        app.MapGet("/convoyage/{yearId:int}/traces/pdf", async (
            int yearId,
            [FromQuery] int? status,
            AppDbContext db,
            IConfiguration config,
            IHostEnvironment env,
            HttpContext http,
            CancellationToken ct) =>
        {
            var year = await db.ConvoyageYears.AsNoTracking()
                .Include(y => y.Site)
                .FirstOrDefaultAsync(y => y.Id == yearId, ct);
            if (year is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(year.Campeonato) || string.IsNullOrWhiteSpace(year.MatriculaTraces))
                return Results.BadRequest("O ano ainda não tem Campeonato + Matrícula TRACES configurados.");

            var q = db.FormSubmissions.AsNoTracking()
                .Where(f => f.FormType == FormType.InscricaoConvoyage && f.ConvoyageYearId == yearId);
            if (status.HasValue) q = q.Where(f => f.Status == (FormStatus)status.Value);
            var ids = await q.OrderBy(f => f.Id).Select(f => f.Id).ToListAsync(ct);

            using var merged = new PdfDocument();
            merged.Info.Title = $"Declarações TRACES — {year.Year}";
            foreach (var id in ids)
            {
                AOB.Application.Forms.TracesPdfResult? r;
                try { r = await AOB.Application.Forms.TracesPdfBuilder.BuildAsync(db, env, config, id, null, ct); }
                catch { continue; }
                if (r is null) continue;

                using var srcStream = new MemoryStream(r.Bytes);
                using var src = PdfReader.Open(srcStream, PdfDocumentOpenMode.Import);
                for (var i = 0; i < src.PageCount; i++)
                    merged.AddPage(src.Pages[i]);
            }

            if (merged.PageCount == 0) return Results.NotFound();

            using var mem = new MemoryStream();
            merged.Save(mem, closeStream: false);
            mem.Position = 0;

            var pdfName = BuildTracesPdfName(year, yearId);
            http.Response.Headers.ContentDisposition = ContentDispositionHeader("attachment", pdfName);
            http.Response.ContentType = "application/pdf";
            http.Response.Headers.CacheControl = "no-store, must-revalidate";
            await mem.CopyToAsync(http.Response.Body, ct);
            return Results.Empty;
        }).RequireAuthorization();

        // Bulk: ZIP com todas as fichas de inscrição do ano (opcionalmente
        // filtradas por estado). Cada PDF é regenerado on-the-fly no idioma
        // pedido (PT/EN) e com ou sem a zona de custos, para permitir enviar
        // aos parceiros sem revelar informação financeira.
        app.MapGet("/convoyage/{yearId:int}/inscricoes/zip", async (
            int yearId,
            [FromQuery] int? status,
            [FromQuery] string? lang,
            [FromQuery] bool? includeCosts,
            [FromQuery] bool? includeTransport,
            AppDbContext db,
            IConfiguration config,
            IHostEnvironment env,
            HttpContext http,
            CancellationToken ct) =>
        {
            var year = await db.ConvoyageYears.AsNoTracking()
                .Include(y => y.Site)
                .FirstOrDefaultAsync(y => y.Id == yearId, ct);
            if (year is null) return Results.NotFound();

            var pdfLang = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
                ? AOB.Application.Forms.PdfLang.En
                : AOB.Application.Forms.PdfLang.Pt;
            var withCosts = includeCosts ?? true;
            var withTransport = includeTransport ?? true;

            var q = db.FormSubmissions.AsNoTracking()
                .Where(f => f.FormType == FormType.InscricaoConvoyage && f.ConvoyageYearId == yearId);
            if (status.HasValue) q = q.Where(f => f.Status == (FormStatus)status.Value);
            var ids = await q.OrderBy(f => f.Id).Select(f => f.Id).ToListAsync(ct);

            using var mem = new MemoryStream();
            using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, leaveOpen: true))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in ids)
                {
                    AOB.Application.Forms.ConvoyageInscricaoPdfResult? r;
                    try { r = await AOB.Application.Forms.ConvoyageInscricaoPdfBuilder.BuildAsync(db, env, config, id, pdfLang, withCosts, withTransport, ct); }
                    catch { continue; }
                    if (r is null) continue;

                    var entryName = $"{id:D5} - {r.FileName}";
                    var candidate = entryName;
                    var n = 1;
                    while (!used.Add(candidate))
                    {
                        n++;
                        var stem = Path.GetFileNameWithoutExtension(entryName);
                        var ext = Path.GetExtension(entryName);
                        candidate = $"{stem} ({n}){ext}";
                    }

                    var entry = zip.CreateEntry(candidate, CompressionLevel.NoCompression);
                    using var s = entry.Open();
                    await s.WriteAsync(r.Bytes, ct);
                }
            }
            mem.Position = 0;

            var zipName = BuildInscricoesZipName(year, yearId, pdfLang, withCosts, withTransport);
            http.Response.Headers.ContentDisposition = ContentDispositionHeader("attachment", zipName);
            http.Response.ContentType = "application/zip";
            http.Response.Headers.CacheControl = "no-store, must-revalidate";
            await mem.CopyToAsync(http.Response.Body, ct);
            return Results.Empty;
        }).RequireAuthorization();

        app.MapGet("/formularios/{id:int}/pdf", async (
            int id,
            AppDbContext db,
            IConfiguration config,
            IHostEnvironment env,
            HttpContext http) =>
        {
            var form = await db.FormSubmissions.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
            if (form is null || form.FormType is not (FormType.InscricaoSocio or FormType.InscricaoConvoyage))
                return Results.NotFound();

            string? relPath = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(form.DataJson);
                if (doc.RootElement.TryGetProperty("PdfPath", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String)
                    relPath = p.GetString();
            }
            catch { }
            if (string.IsNullOrWhiteSpace(relPath)) return Results.NotFound();

            var storageRoot = config["Inscricoes:StorageRoot"];
            if (string.IsNullOrWhiteSpace(storageRoot))
                storageRoot = Path.Combine(env.ContentRootPath, "PrivateData", "inscricoes");
            storageRoot = Path.GetFullPath(storageRoot);

            var abs = Path.GetFullPath(Path.Combine(storageRoot, relPath));
            if (!abs.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();
            if (!File.Exists(abs)) return Results.NotFound();

            var bytes = await File.ReadAllBytesAsync(abs);
            var name = Path.GetFileName(abs);
            http.Response.Headers.ContentDisposition = ContentDispositionHeader("inline", name);
            http.Response.ContentType = "application/pdf";
            // Sem cache: o PDF pode ser regenerado depois de edições, pelo que
            // o browser deve puxar sempre a versão actual em disco.
            http.Response.Headers.CacheControl = "no-store, must-revalidate";
            http.Response.Headers.Pragma = "no-cache";
            await http.Response.Body.WriteAsync(bytes);
            return Results.Empty;
        }).RequireAuthorization();

        return app;
    }

    // Defesa CSRF para endpoints com .DisableAntiforgery(): so aceita pedidos
    // cujo Origin/Referer aponta para o mesmo host do backoffice. Em conjunto
    // com o cookie SameSite=Lax, isto impede POSTs multipart de outras origens.
    private static bool IsSameOrigin(HttpRequest req)
    {
        var host = req.Host.ToString();
        if (string.IsNullOrEmpty(host)) return false;

        static bool HostMatches(string url, string expectedHost)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                   string.Equals(u.Authority, expectedHost, StringComparison.OrdinalIgnoreCase);
        }

        if (req.Headers.TryGetValue("Origin", out var origin) && !string.IsNullOrEmpty(origin))
            return HostMatches(origin.ToString(), host);

        if (req.Headers.TryGetValue("Referer", out var referer) && !string.IsNullOrEmpty(referer))
            return HostMatches(referer.ToString(), host);

        // Sem Origin nem Referer: bloqueia (browsers modernos enviam pelo menos um em POST).
        return false;
    }

    private static async Task<string> MakeUniqueSlug(AppDbContext db, int siteId, string baseSlug)
    {
        var candidate = baseSlug;
        var n = 1;
        while (await db.Downloads.AsNoTracking().AnyAsync(d => d.SiteId == siteId && d.Slug == candidate))
        {
            n++;
            candidate = $"{baseSlug}-{n}";
        }
        return candidate;
    }

    private static string BuildInscricoesZipName(
        ConvoyageYear? year, int fallbackId,
        AOB.Application.Forms.PdfLang lang, bool includeCosts, bool includeTransport)
    {
        var parts = new List<string> { "convoyage" };
        if (year is not null)
        {
            var siteSlug = SlugFrom(year.Site?.Slug ?? year.Site?.Name ?? "");
            if (!string.IsNullOrEmpty(siteSlug)) parts.Add(siteSlug);
            parts.Add(year.Year.ToString());
            var desc = SlugFrom(year.Description ?? "");
            if (!string.IsNullOrEmpty(desc)) parts.Add(desc);
        }
        else
        {
            parts.Add(fallbackId.ToString());
        }
        parts.Add("inscricoes");
        parts.Add(lang == AOB.Application.Forms.PdfLang.En ? "en" : "pt");
        if (!includeCosts) parts.Add("sem-custos");
        if (!includeTransport) parts.Add("sem-transporte");
        return string.Join("-", parts) + ".zip";
    }

    private static string BuildTracesPdfName(ConvoyageYear? year, int fallbackId)
    {
        if (year is null) return $"convoyage-{fallbackId}-traces.pdf";
        var parts = new List<string> { "convoyage" };
        var siteRaw = year.Site?.Slug ?? year.Site?.Name ?? "";
        if (!string.IsNullOrWhiteSpace(siteRaw)) parts.Add(SlugFrom(siteRaw));
        parts.Add(year.Year.ToString());
        var descRaw = year.Description ?? "";
        if (!string.IsNullOrWhiteSpace(descRaw)) parts.Add(SlugFrom(descRaw));
        parts.Add("traces");
        return string.Join("-", parts) + ".pdf";
    }

    // Kestrel rejeita non-ASCII (ex.: "é" 0xE9) em headers. RFC 6266/5987 exige
    // `filename` ASCII e `filename*=UTF-8''<pct-encoded>` para o nome real. O
    // ContentDispositionHeaderValue faz o encoding correcto quando definimos
    // FileNameStar; FileName leva um fallback ASCII para clientes antigos.
    private static string ContentDispositionHeader(string disposition, string fileName)
    {
        var ascii = new string(fileName.Select(c => c < 128 ? c : '_').ToArray());
        return new ContentDispositionHeaderValue(disposition)
        {
            FileName = ascii,
            FileNameStar = fileName,
        }.ToString();
    }

    private static string SlugFrom(string s)
    {
        var lower = s.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch == ' ' || ch == '-' || ch == '_') sb.Append('-');
        }
        var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug)) slug = Guid.NewGuid().ToString("N")[..8];
        return slug;
    }
}
