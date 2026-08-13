using System.Text.Json;
using System.Text.RegularExpressions;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands;

/// <summary>
/// Pré-popula <c>Site.Tagline</c> e <c>Site.HomeConfig</c> a partir do conteúdo já migrado.
/// Não sobrescreve valores existentes — o admin refina depois no backoffice.
///
/// Estratégia:
///  - Tagline: primeira frase da descrição do site (se vazia, fallback fixo por slug).
///  - HomeConfig.mission: primeiros 400 chars (sanitizados) do artigo mais antigo em categorias
///    com slug tipo 'quem-somos', 'clube', 'institucional', 'historia'.
///  - Areas: defaults sensatos por site (AOB: Anilhas/Exposições/Formação/Ser sócio;
///    BVA: Standards/Comissão técnica/Exposições/Revista).
///  - CTA e stats: deixa vazios (o admin preenche com dados reais).
/// </summary>
public class SeedHomeContentCommand(AppDbContext db, ILogger<SeedHomeContentCommand> log)
{
    private static readonly string[] MissionCategorySlugs =
    {
        "quem-somos", "clube", "institucional", "historia", "sobre", "sobre-nos", "orgaos-sociais",
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task RunAsync()
    {
        log.LogInformation("=== Seed home content ===");

        var sites = await db.Sites.ToListAsync();
        foreach (var site in sites)
        {
            log.LogInformation("--- Site: {Slug} ---", site.Slug);

            // Tagline
            if (string.IsNullOrWhiteSpace(site.Tagline))
            {
                var derived = DeriveTagline(site.Description) ?? DefaultTagline(site.Slug);
                if (!string.IsNullOrWhiteSpace(derived))
                {
                    site.Tagline = derived;
                    log.LogInformation("Tagline definido: {Tagline}", derived);
                }
            }
            else
            {
                log.LogInformation("Tagline já definido — mantido.");
            }

            // HomeConfig
            var homeJson = string.IsNullOrWhiteSpace(site.HomeConfig)
                ? new Dictionary<string, object?>()
                : JsonSerializer.Deserialize<Dictionary<string, object?>>(site.HomeConfig) ?? new();

            if (!homeJson.ContainsKey("mission") || homeJson["mission"] is null
                || (homeJson["mission"] is JsonElement je && je.ValueKind == JsonValueKind.String
                    && string.IsNullOrWhiteSpace(je.GetString())))
            {
                var mission = await ExtractMissionAsync(site.Id);
                if (!string.IsNullOrWhiteSpace(mission))
                {
                    homeJson["mission"] = mission;
                    log.LogInformation("Missão pré-populada ({Chars} chars)", mission.Length);
                }
            }

            if (!homeJson.ContainsKey("areas") || homeJson["areas"] is null
                || (homeJson["areas"] is JsonElement ja && ja.ValueKind == JsonValueKind.Array && ja.GetArrayLength() == 0))
            {
                homeJson["areas"] = DefaultAreas(site.Slug);
                log.LogInformation("Áreas de atuação defaults inseridas.");
            }

            if (!homeJson.ContainsKey("ctaTitle") || homeJson["ctaTitle"] is null)
            {
                homeJson["ctaTitle"] = "Junta-te à associação";
                homeJson["ctaSubtitle"] = "Faz parte de uma comunidade de criadores e ornitólogos.";
                homeJson["ctaHref"] = "/inscricao-socio";
                homeJson["ctaLabel"] = "Quero ser sócio";
            }

            site.HomeConfig = JsonSerializer.Serialize(homeJson, JsonOpts);
        }

        await db.SaveChangesAsync();
        log.LogInformation("=== Home content OK ===");
    }

    private async Task<string?> ExtractMissionAsync(int siteId)
    {
        var catIds = await db.Categories
            .Where(c => c.SiteId == siteId && MissionCategorySlugs.Contains(c.Slug))
            .Select(c => c.Id)
            .ToListAsync();

        if (catIds.Count == 0) return null;

        var article = await db.Articles
            .Where(a => a.SiteId == siteId && catIds.Contains(a.CategoryId) && a.IsPublished)
            .OrderBy(a => a.PublishedAt ?? a.CreatedAt)
            .FirstOrDefaultAsync();

        if (article is null) return null;
        return CleanText(article.Excerpt) ?? CleanText(article.Content);
    }

    private static string? CleanText(string? raw, int max = 400)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = Regex.Replace(raw, @"\{[^{}]*\}", " ");
        t = Regex.Replace(t, @"<[^>]+>", " ");
        t = System.Net.WebUtility.HtmlDecode(t);
        t = Regex.Replace(t, @"\s+", " ").Trim();
        if (t.Length == 0) return null;
        if (t.Length > max)
        {
            var cut = t[..max];
            var lastSpace = cut.LastIndexOf(' ');
            if (lastSpace > max / 2) cut = cut[..lastSpace];
            t = cut + "…";
        }
        return t;
    }

    private static string? DeriveTagline(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var first = description.Split(new[] { '.', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                               .FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(first)) return null;
        return first.Length > 300 ? first[..300] : first;
    }

    private static string DefaultTagline(string slug) => slug switch
    {
        "aob" => "Associação Ornitológica de Barcelos — a casa dos criadores e amantes das aves.",
        "bva" => "BVA Portugal — a associação portuguesa de Agapornis.",
        _ => "",
    };

    private static object DefaultAreas(string slug)
    {
        if (slug == "aob")
        {
            return new[]
            {
                new { icon = "Award",    title = "Anilhas oficiais", description = "Emissão e gestão de anilhas oficiais para criadores registados.", href = "/pedidos-anilhas" },
                new { icon = "Calendar", title = "Exposições",       description = "Organização de exposições e concursos anuais.",                    href = "/categoria/exposicoes" },
                new { icon = "BookOpen", title = "Formação",         description = "Materiais técnicos, standards e boas práticas de criação.",         href = "/downloads" },
                new { icon = "Users",    title = "Ser sócio",        description = "Junta-te a uma comunidade com décadas de tradição em Barcelos.",   href = "/inscricao-socio" },
            };
        }
        if (slug == "bva")
        {
            return new[]
            {
                new { icon = "Award",  title = "Standards",               description = "Padrões oficiais das mutações de Agapornis reconhecidas.",                                    href = "/categoria/standards" },
                new { icon = "Trophy", title = "Exposição & Julgamento",  description = "Exposição nacional com julgamento técnico certificado pela comissão da BVA Internacional.",  href = "/categoria/exposicoes" },
                new { icon = "Dna",   title = "Sexagem de aves",          description = "Sexagem de aves por ADN a preço especial para sócios BVA.",                                 href = "/categoria/sexagem" },
                new { icon = "Plane", title = "BVA Masters",              description = "Convoyage portuguesa à maior exposição temática de Agapornis da Europa.",                   href = "/categoria/bva-masters" },
            };
        }
        return Array.Empty<object>();
    }
}
