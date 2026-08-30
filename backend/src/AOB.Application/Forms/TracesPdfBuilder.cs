using System.Text.Json;
using AOB.Application.Contracts;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AOB.Application.Forms;

// Constrói a Declaração TRACES de uma inscrição de convoyage, regenerando
// sempre a partir dos dados actuais do ano + assinatura persistida em disco.
// Chamado tanto pelo endpoint público (com HMAC) como pelo endpoint admin.
public record TracesPdfResult(byte[] Bytes, string FileName);

public static class TracesPdfBuilder
{
    public static async Task<TracesPdfResult?> BuildAsync(
        AppDbContext db,
        IHostEnvironment env,
        IConfiguration config,
        int submissionId,
        string? expectedSiteSlug,
        CancellationToken ct = default)
    {
        var submission = await db.FormSubmissions
            .AsNoTracking()
            .Include(s => s.Site)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.FormType == FormType.InscricaoConvoyage, ct);
        if (submission is null) return null;
        if (expectedSiteSlug is not null &&
            !string.Equals(submission.Site.Slug, expectedSiteSlug, StringComparison.OrdinalIgnoreCase))
            return null;
        if (submission.ConvoyageYearId is null) return null;

        var year = await db.ConvoyageYears
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == submission.ConvoyageYearId.Value, ct);
        if (year is null) return null;
        if (string.IsNullOrWhiteSpace(year.Campeonato) || string.IsNullOrWhiteSpace(year.MatriculaTraces))
            return null;

        string nome = "", email = "", telefone = "", numeroStam = "", morada = "", cp = "", loc = "";
        string? assinaturaRel = null;
        try
        {
            using var doc = JsonDocument.Parse(submission.DataJson);
            var root = doc.RootElement;
            nome = GetStr(root, "NomeCompleto") ?? "";
            email = GetStr(root, "Email") ?? "";
            telefone = GetStr(root, "Telefone") ?? "";
            numeroStam = GetStr(root, "NumeroStam") ?? "";
            morada = GetStr(root, "Morada") ?? "";
            cp = GetStr(root, "CodigoPostal") ?? "";
            loc = GetStr(root, "Localidade") ?? "";
            assinaturaRel = GetStr(root, "AssinaturaPath");
        }
        catch { }

        if (string.IsNullOrWhiteSpace(assinaturaRel)) return null;

        var storageRoot = config["Inscricoes:StorageRoot"];
        if (string.IsNullOrWhiteSpace(storageRoot))
            storageRoot = Path.Combine(env.ContentRootPath, "PrivateData", "inscricoes");
        var rootFull = Path.GetFullPath(storageRoot);
        var absSig = Path.GetFullPath(Path.Combine(storageRoot, assinaturaRel));
        if (!absSig.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) return null;
        if (!File.Exists(absSig)) return null;
        var sigBytes = await File.ReadAllBytesAsync(absSig, ct);

        var aves = await db.ConvoyageBirdEntries
            .AsNoTracking()
            .Where(e => e.FormSubmissionId == submissionId)
            .Include(e => e.NomenclatureClass).ThenInclude(c => c.NomenclatureGroup)
            .OrderBy(e => e.BirdOrder)
            .Select(e => new
            {
                Species = e.NomenclatureClass.NomenclatureGroup.Species,
                e.RingNumber,
            })
            .ToListAsync(ct);
        var especieAnilha = new List<(string, string)>();
        especieAnilha.AddRange(aves.Select(a => (SpeciesGenus.Full(a.Species), a.RingNumber)));

        // Aves para venda e aves de transporte (compra/venda) — vêm apenas do
        // snapshot em DataJson (não há tabela estruturada). Todas entram na
        // declaração TRACES: as de venda/vende viajam com o criador na ida, as
        // de compra viajam com ele no retorno; o documento cobre o transporte.
        try
        {
            using var doc = JsonDocument.Parse(submission.DataJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("AvesVenda", out var vArr) && vArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in vArr.EnumerateArray())
                {
                    var esp = GetStr(a, "Especie") ?? "";
                    var an = GetStr(a, "Anilha") ?? "";
                    if (!string.IsNullOrWhiteSpace(an))
                        especieAnilha.Add((FormatEspecie(esp), an.Trim()));
                }
            }
            if (root.TryGetProperty("AvesTransporte", out var tArr) && tArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in tArr.EnumerateArray())
                {
                    var esp = GetStr(a, "Especie") ?? "";
                    var an = GetStr(a, "Anilha") ?? "";
                    if (!string.IsNullOrWhiteSpace(an))
                        especieAnilha.Add((FormatEspecie(esp), an.Trim()));
                }
            }
        }
        catch { }

        // Tenta primeiro ContentRootPath (funciona para AOB.Api em dev) e cai
        // para AppContext.BaseDirectory (funciona para AOB.Admin em dev — o
        // csproj copia os PNGs de ../AOB.Api/PdfAssets/ apenas para o bin/).
        async Task<byte[]?> LoadAsset(string fileName)
        {
            var p1 = Path.Combine(env.ContentRootPath, "PdfAssets", fileName);
            if (File.Exists(p1)) return await File.ReadAllBytesAsync(p1, ct);
            var p2 = Path.Combine(AppContext.BaseDirectory, "PdfAssets", fileName);
            if (File.Exists(p2)) return await File.ReadAllBytesAsync(p2, ct);
            return null;
        }
        var fonpLogo = await LoadAsset("logo-fonp.png");
        var bvaLogo = await LoadAsset($"logo-{submission.Site.Slug}.png");

        var req = new InscricaoConvoyageRequest(
            NomeCompleto: nome,
            Email: email,
            Telefone: telefone,
            Pais: "",
            NumeroStam: numeroStam,
            LocalRecolhaId: 0,
            AceitouRegulamento: true,
            SocioBvaStatus: SocioBvaStatus.NaoSocio,
            Aves: new List<AveConvoyageDto>(),
            AvesVenda: null,
            AvesTransporte: null,
            TurnstileToken: null,
            Morada: morada,
            CodigoPostal: cp,
            Localidade: loc,
            AssinaturaPngBase64: null);

        var bytes = TracesDeclarationPdfGenerator.Render(
            req, year.Campeonato!, year.MatriculaTraces!,
            especieAnilha, sigBytes, fonpLogo, bvaLogo);

        var safeNome = SafeFileName(nome);
        var fileName = string.IsNullOrWhiteSpace(safeNome)
            ? $"TRACES-{submissionId}.pdf"
            : $"TRACES - {safeNome}.pdf";
        return new TracesPdfResult(bytes, fileName);
    }

    private static string? GetStr(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString();
    }

    // Normaliza espécie: se o valor corresponde a um SpeciesCode, devolve o
    // nome binomial completo com género correcto ("Agapornis Roseicollis",
    // "Forpus Coelestis"). Free-text (venda com espécie livre) é preservado.
    private static string FormatEspecie(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(s)) return "";
        return Enum.TryParse<SpeciesCode>(s, ignoreCase: true, out var code)
            ? SpeciesGenus.Full(code)
            : s;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string((name ?? "").Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return clean.Length > 100 ? clean[..100] : clean;
    }
}
