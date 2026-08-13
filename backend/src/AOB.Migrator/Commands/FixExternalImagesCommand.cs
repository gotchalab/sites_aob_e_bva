using AOB.Infrastructure.Persistence;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands;

/// <summary>
/// Varre todos os artigos da PG local à procura de imagens externas (http/https) em
/// <c>CoverImagePath</c> ou em <c>Content</c>, tenta fazer download para
/// uploads-target/{site}/images/imported/ e reescreve as referências para paths locais.
/// Se o download falhar (URL morta, 403, timeout), limpa o campo em vez de deixar link partido.
/// Idempotente — safe para correr várias vezes.
/// </summary>
public class FixExternalImagesCommand(
    AppDbContext db,
    ExternalImageDownloader downloader,
    ILogger<FixExternalImagesCommand> log)
{
    // Tamanho mínimo (bytes) para uma imagem do corpo poder ser promovida a capa.
    // Emojis de Facebook (emoji.php) e ícones tipicamente < 5 KB.
    private const int MinCoverPromotionBytes = 8 * 1024;

    public async Task RunAsync()
    {
        log.LogInformation("=== Fix external images ===");

        var sites = await db.Sites.ToDictionaryAsync(s => s.Id, s => s.Slug);

        var articles = await db.Articles
            .Where(a =>
                (a.CoverImagePath != null && (a.CoverImagePath.StartsWith("http://") || a.CoverImagePath.StartsWith("https://")))
                || a.Content.Contains("src=\"http://") || a.Content.Contains("src=\"https://")
                || a.Content.Contains("src='http://") || a.Content.Contains("src='https://"))
            .ToListAsync();

        log.LogInformation("Encontrados {Count} artigos com imagens externas potenciais.", articles.Count);

        int fixedCovers = 0, clearedCovers = 0, fixedBodyImgs = 0, failedBodyImgs = 0;

        foreach (var art in articles)
        {
            if (!sites.TryGetValue(art.SiteId, out var siteSlug))
                continue;

            // 1) Cover externo
            if (!string.IsNullOrWhiteSpace(art.CoverImagePath) &&
                (art.CoverImagePath.StartsWith("http://") || art.CoverImagePath.StartsWith("https://")))
            {
                var local = await downloader.DownloadAsync(art.CoverImagePath, siteSlug);
                if (local != null)
                {
                    log.LogInformation("[{Slug}] cover: {Old} -> {New}", art.Slug, art.CoverImagePath, local);
                    art.CoverImagePath = local;
                    fixedCovers++;
                }
                else
                {
                    log.LogWarning("[{Slug}] cover externo morto — a limpar: {Url}", art.Slug, art.CoverImagePath);
                    art.CoverImagePath = null;
                    clearedCovers++;
                }
            }

            // 2) <img src="http..."> no corpo
            var (newContent, promoted, ok, ko) = await RewriteBodyImgsAsync(art.Content, siteSlug);
            if (newContent != art.Content)
            {
                art.Content = newContent;
                art.UpdatedAt = DateTime.UtcNow;
                fixedBodyImgs += ok;
                failedBodyImgs += ko;

                // Só promove a capa imagens com peso mínimo (evita emojis / ícones).
                if (string.IsNullOrWhiteSpace(art.CoverImagePath) && promoted != null)
                {
                    art.CoverImagePath = promoted;
                    log.LogInformation("[{Slug}] cover promovido do corpo: {Path}", art.Slug, promoted);
                    fixedCovers++;
                }
            }
        }

        await db.SaveChangesAsync();

        log.LogInformation("=== Fim ===");
        log.LogInformation("Covers reescritos: {C}", fixedCovers);
        log.LogInformation("Covers limpos (URL morta): {C}", clearedCovers);
        log.LogInformation("Imagens no corpo reescritas: {C}", fixedBodyImgs);
        log.LogInformation("Imagens no corpo com download falhado: {C}", failedBodyImgs);
    }

    private async Task<(string Html, string? PromotedCover, int Ok, int Failed)> RewriteBodyImgsAsync(
        string html, string siteSlug)
    {
        if (string.IsNullOrWhiteSpace(html) || (!html.Contains("src=\"http") && !html.Contains("src='http")))
            return (html, null, 0, 0);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var imgs = doc.DocumentNode.SelectNodes("//img[@src]");
        if (imgs == null || imgs.Count == 0)
            return (html, null, 0, 0);

        int ok = 0, failed = 0;
        string? firstLargeLocal = null;
        bool changed = false;

        foreach (var img in imgs)
        {
            var src = img.GetAttributeValue("src", "");
            if (string.IsNullOrWhiteSpace(src)) continue;
            if (!src.StartsWith("http://") && !src.StartsWith("https://")) continue;

            var (local, bytes) = await downloader.DownloadWithSizeAsync(src, siteSlug);
            if (local != null)
            {
                img.SetAttributeValue("src", local);
                changed = true;
                ok++;
                if (firstLargeLocal == null && bytes >= MinCoverPromotionBytes)
                    firstLargeLocal = local;
            }
            else
            {
                // Remove img partida (evita ícone de imagem quebrada no artigo)
                img.Remove();
                changed = true;
                failed++;
            }
        }

        return (changed ? doc.DocumentNode.InnerHtml : html, firstLargeLocal, ok, failed);
    }
}
