using System.Text.Json;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands;

/// <summary>
/// Força a actualização do HomeConfig do site BVA na BD:
///   - areas: Standards / Comissão técnica (BVA Internacional) / Exposições / Sexagem / BVA Masters
///   - ctaTitle / ctaSubtitle / ctaHref / ctaLabel (se ainda forem os defaults genéricos)
/// Reexecutável sem efeitos adversos — sobrescreve sempre o campo "areas".
/// </summary>
public class PatchBvaHomeCommand(AppDbContext db, ILogger<PatchBvaHomeCommand> log)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
    };

    private static readonly object[] NewAreas =
    [
        new { icon = "Award",   title = "Standards",                description = "Padrões oficiais das mutações de Agapornis reconhecidas.",                                        href = (string?)null },
        new { icon = "Trophy",  title = "Exposições & Julgamento",  description = "Exposições nacionais com julgamento técnico certificado pela comissão da BVA Internacional.",    href = (string?)null },
        new { icon = "Dna",     title = "Sexagem de aves",          description = "Sexagem de aves por ADN a preço especial para sócios BVA.",                                       href = (string?)null },
        new { icon = "Plane",   title = "BVA Masters",              description = "Convoyage portuguesa à maior exposição temática de Agapornis da Europa.",                         href = (string?)null },
    ];

    private static readonly object[] NewBenefits =
    [
        new { icon = "Award",       label = "Standards oficiais",      description = "Aceder aos padrões técnicos reconhecidos internacionalmente." },
        new { icon = "Calendar",    label = "Acesso a exposições",      description = "Participar nas edições BVA e nos concursos parceiros." },
        new { icon = "ShieldCheck", label = "Área reservada online",    description = "Gerir quotas, dados e pedidos de anilhas num só sítio." },
        new { icon = "Users",       label = "Comunidade técnica",       description = "Ligação directa a criadores e juízes de Agapornis." },
        new { icon = "Plane",       label = "Convoyage à BVA Masters",  description = "Leva as tuas aves à maior exposição temática de Agapornis da Europa, com a delegação portuguesa." },
    ];

    public async Task RunAsync()
    {
        log.LogInformation("=== PatchBvaHome: início ===");

        var site = await db.Sites.FirstOrDefaultAsync(s => s.Slug == "bva");
        if (site is null)
        {
            log.LogError("Site com slug 'bva' não encontrado na BD.");
            return;
        }

        var homeJson = string.IsNullOrWhiteSpace(site.HomeConfig)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(site.HomeConfig) ?? new();

        homeJson["areas"] = NewAreas;
        log.LogInformation("Áreas actualizadas ({Count} itens).", NewAreas.Length);

        homeJson["benefits"] = NewBenefits;
        log.LogInformation("Benefits actualizados ({Count} itens).", NewBenefits.Length);

        // Preenche CTA se ainda estiver vazio ou for o default genérico
        var ctaTitle = homeJson.TryGetValue("ctaTitle", out var ct) && ct is JsonElement cte
            ? cte.GetString() : null;
        if (string.IsNullOrWhiteSpace(ctaTitle) || ctaTitle == "Junta-te à associação")
        {
            homeJson["ctaTitle"]    = "Junta-te à BVA Portugal";
            homeJson["ctaSubtitle"] = "Faz parte da associação técnica portuguesa de Agapornis e liga-te à comunidade nacional de criadores.";
            homeJson["ctaHref"]     = "/inscricao-socio";
            homeJson["ctaLabel"]    = "Quero ser sócio";
            log.LogInformation("CTA actualizado.");
        }

        site.HomeConfig = JsonSerializer.Serialize(homeJson, JsonOpts);
        await db.SaveChangesAsync();

        log.LogInformation("=== PatchBvaHome: concluído ===");
    }
}
