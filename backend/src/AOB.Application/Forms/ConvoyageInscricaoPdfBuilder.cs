using System.Text.Json;
using AOB.Application.Contracts;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AOB.Application.Forms;

// Regenera o PDF de uma inscrição de convoyage a partir do snapshot em
// DataJson + tabela ConvoyageBirdEntries. Suporta escolha de idioma (PT/EN)
// e inclusão opcional da zona de custos. Usado pelo endpoint de download
// bulk (ZIP) para permitir múltiplas variantes sem regravar o ficheiro em
// disco (o PDF gravado no submit continua a ser o master em PT/com custos).
public record ConvoyageInscricaoPdfResult(byte[] Bytes, string FileName);

public static class ConvoyageInscricaoPdfBuilder
{
    public static async Task<ConvoyageInscricaoPdfResult?> BuildAsync(
        AppDbContext db,
        IHostEnvironment env,
        IConfiguration config,
        int submissionId,
        PdfLang lang,
        bool includeCosts,
        CancellationToken ct = default)
    {
        var submission = await db.FormSubmissions.AsNoTracking()
            .Include(s => s.Site)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.FormType == FormType.InscricaoConvoyage, ct);
        if (submission is null) return null;

        ConvoyageYear? year = null;
        if (submission.ConvoyageYearId is int cyId)
        {
            year = await db.ConvoyageYears.AsNoTracking()
                .Include(y => y.CollectionPoints)
                .FirstOrDefaultAsync(y => y.Id == cyId, ct);
        }

        var point = year?.CollectionPoints.FirstOrDefault(p => p.Id == (submission.LocalRecolhaId ?? 0));

        InscricaoConvoyageRequest? req;
        string localRecolhaLabel;
        int convoyageYear;
        try
        {
            req = ParseRequest(submission.DataJson);
            if (req is null) return null;

            localRecolhaLabel = point is not null
                ? $"{point.Name}{(point.Location is not null ? $" ({point.Location})" : "")}"
                : ExtractLocalRecolha(submission.DataJson);

            convoyageYear = year?.Year ?? ExtractConvoyageYear(submission.DataJson);
        }
        catch
        {
            return null;
        }

        byte[]? logoBytes = null;
        var logoFile = $"logo-{submission.Site.Slug}.png";
        string[] logoCandidates =
        {
            Path.Combine(env.ContentRootPath, "PdfAssets", logoFile),
            Path.Combine(AppContext.BaseDirectory, "PdfAssets", logoFile),
        };
        foreach (var p in logoCandidates)
        {
            if (!File.Exists(p)) continue;
            try { logoBytes = await File.ReadAllBytesAsync(p, ct); break; }
            catch { /* logo é opcional */ }
        }

        byte[] bytes;
        try
        {
            bytes = InscricaoConvoyagePdfGenerator.Render(
                submission.Site, req, submission.Id, localRecolhaLabel, convoyageYear,
                logoBytes, lang, includeCosts);
        }
        catch
        {
            return null;
        }

        var safeNome = SafeFileName(req.NomeCompleto);
        var prefix = lang == PdfLang.En ? "Registration" : "Inscricao";
        var fileName = string.IsNullOrWhiteSpace(safeNome)
            ? $"{prefix}-{submissionId}.pdf"
            : $"{prefix} - {safeNome}.pdf";
        return new ConvoyageInscricaoPdfResult(bytes, fileName);
    }

    private static InscricaoConvoyageRequest? ParseRequest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;

        string S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
        string? SN(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        bool B(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

        // SocioBvaStatus vem serializado como string no JSON (JsonStringEnumConverter).
        var statusStr = S("SocioBvaStatus");
        var status = Enum.TryParse<SocioBvaStatus>(statusStr, ignoreCase: true, out var st)
            ? st
            : (B("SocioBva") ? SocioBvaStatus.JaSocio : SocioBvaStatus.NaoSocio);

        var aves = new List<AveConvoyageDto>();
        if (r.TryGetProperty("Aves", out var avesEl) && avesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in avesEl.EnumerateArray())
            {
                string AS(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                Guid? AG(string k)
                {
                    if (!a.TryGetProperty(k, out var v)) return null;
                    if (v.ValueKind != JsonValueKind.String) return null;
                    return Guid.TryParse(v.GetString(), out var g) ? g : null;
                }
                aves.Add(new AveConvoyageDto(
                    AS("Serie"), AS("EspecieMutacao"), AS("Especie"),
                    AS("TipoClasse"), AS("Anilha"),
                    AG("EquipaId"), AS("PosicaoEquipa")));
            }
        }

        var venda = new List<AveVendaDto>();
        if (r.TryGetProperty("AvesVenda", out var vEl) && vEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in vEl.EnumerateArray())
            {
                string AS(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                bool AB(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
                decimal AD(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;

                SexoAve sexo = SexoAve.Indefinido;
                var sx = AS("Sexo");
                if (Enum.TryParse<SexoAve>(sx, ignoreCase: true, out var pv)) sexo = pv;
                else if (sx == "M") sexo = SexoAve.Macho;
                else if (sx == "F") sexo = SexoAve.Femea;

                venda.Add(new AveVendaDto(
                    AS("Especie"), AS("TipoClasse"), AS("EspecieMutacao"), AB("EspecieLivre"),
                    AS("DataNascimento"), sexo, AD("Preco"), AS("Anilha")));
            }
        }

        var transporte = new List<AveTransporteDto>();
        if (r.TryGetProperty("AvesTransporte", out var tEl) && tEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in tEl.EnumerateArray())
            {
                string AS(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                string? ASN(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                var origemStr = AS("Origem");
                var origem = string.Equals(origemStr, "Compra", StringComparison.OrdinalIgnoreCase)
                    ? OrigemAveTransporte.Compra
                    : OrigemAveTransporte.Vende;

                transporte.Add(new AveTransporteDto(
                    AS("Especie"), origem, AS("Anilha"),
                    AS("DestinatarioNome"), AS("DestinatarioWhatsapp"),
                    ASN("DestinatarioNotas")));
            }
        }

        return new InscricaoConvoyageRequest(
            NomeCompleto: S("NomeCompleto"),
            Email: S("Email"),
            Telefone: SN("Telefone"),
            Pais: S("Pais"),
            NumeroStam: SN("NumeroStam"),
            LocalRecolhaId: 0,
            AceitouRegulamento: r.TryGetProperty("AceitouRegulamento", out var ar)
                ? (ar.ValueKind == JsonValueKind.True)
                : true,
            SocioBvaStatus: status,
            Aves: aves,
            AvesVenda: venda.Count == 0 ? null : venda,
            AvesTransporte: transporte.Count == 0 ? null : transporte,
            TurnstileToken: null,
            Morada: SN("Morada"),
            CodigoPostal: SN("CodigoPostal"),
            Localidade: SN("Localidade"),
            AssinaturaPngBase64: null,
            DeclaraArt59: B("DeclaraArt59"));
    }

    private static string ExtractLocalRecolha(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.TryGetProperty("LocalRecolha", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
        }
        catch { }
        return "";
    }

    private static int ExtractConvoyageYear(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.TryGetProperty("ConvoyageYear", out var v) && v.ValueKind == JsonValueKind.Number)
                return v.GetInt32();
        }
        catch { }
        return DateTime.UtcNow.Year;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string((name ?? "").Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return clean.Length > 100 ? clean[..100] : clean;
    }
}
