using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
            http.Response.Headers.ContentDisposition = $"inline; filename=\"{name}\"";
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
