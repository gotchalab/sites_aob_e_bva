using System.Text.Json;
using AOB.Application.Contracts;
using AOB.Application.Forms;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AOB.Admin.Services;

public class FormAdminService(AppDbContext db, IConfiguration config, IHostEnvironment env, EmailSender email)
{
    public Task<List<FormSubmission>> ListAsync(
        int siteId, FormStatus? status, FormType? type,
        int page, int pageSize,
        FormType[]? excludeTypes = null) =>
        BuildQuery(siteId, status, type, excludeTypes)
            .OrderByDescending(f => f.SubmittedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

    public Task<int> CountAsync(
        int siteId, FormStatus? status, FormType? type,
        FormType[]? excludeTypes = null) =>
        BuildQuery(siteId, status, type, excludeTypes).CountAsync();

    private IQueryable<FormSubmission> BuildQuery(
        int siteId, FormStatus? status, FormType? type, FormType[]? excludeTypes)
    {
        var q = db.FormSubmissions.AsNoTracking()
            .Where(f => f.SiteId == siteId);
        if (status.HasValue) q = q.Where(f => f.Status == status.Value);
        if (type.HasValue) q = q.Where(f => f.FormType == type.Value);
        if (excludeTypes is { Length: > 0 })
            q = q.Where(f => !excludeTypes.Contains(f.FormType));
        return q;
    }

    public Task<FormSubmission?> GetAsync(int id) =>
        db.FormSubmissions.FirstOrDefaultAsync(f => f.Id == id);

    public async Task SetStatusAsync(int id, FormStatus status, string? handledById, string? notes)
    {
        var f = await db.FormSubmissions.FindAsync(id);
        if (f is null) return;
        f.Status = status;
        f.HandledById = handledById;
        f.HandledAt = DateTime.UtcNow;
        if (notes is not null) f.Notes = notes;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var f = await db.FormSubmissions.FindAsync(id);
        if (f is null) return;

        TryDeleteAttachedFiles(f);

        db.FormSubmissions.Remove(f);
        await db.SaveChangesAsync();
    }

    private void TryDeleteAttachedFiles(FormSubmission f)
    {
        // PDF (e comprovativo, para inscrição de sócio) são guardados em disco
        // fora do wwwroot. Se existir referência no DataJson, apagar também.
        try
        {
            using var doc = JsonDocument.Parse(f.DataJson);
            var root = doc.RootElement;
            var storageRoot = ResolveStorageRoot();

            void TryDelete(string prop)
            {
                if (!root.TryGetProperty(prop, out var v)) return;
                if (v.ValueKind != JsonValueKind.String) return;
                var rel = v.GetString();
                if (string.IsNullOrWhiteSpace(rel)) return;

                var abs = Path.GetFullPath(Path.Combine(storageRoot, rel));
                if (!abs.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(abs)) File.Delete(abs);
            }

            TryDelete("PdfPath");
            TryDelete("ComprovativoPath");
        }
        catch
        {
            // Falha a apagar ficheiro não deve bloquear o delete da linha.
        }
    }

    private string ResolveStorageRoot()
    {
        var storageRoot = config["Inscricoes:StorageRoot"];
        if (string.IsNullOrWhiteSpace(storageRoot))
            storageRoot = Path.Combine(env.ContentRootPath, "PrivateData", "inscricoes");
        return Path.GetFullPath(storageRoot);
    }

    // ── Nomenclatura para dropdowns da página de edição ──────────────────────

    public record NomClass(string Code, string Mutation);
    public record NomGroup(
        string Species, string EntryType, string DisplayName, List<NomClass> Classes);

    public async Task<List<NomGroup>> GetNomenclatureForYearAsync(int convoyageYearId)
    {
        var rows = await db.NomenclatureGroups
            .AsNoTracking()
            .Where(g => g.ConvoyageYearId == convoyageYearId)
            .OrderBy(g => g.SortOrder)
            .Select(g => new
            {
                Species = g.Species.ToString(),
                EntryType = g.EntryType.ToString(),
                g.DisplayName,
                Classes = g.Classes
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new NomClass(c.Code, c.Mutation))
                    .ToList(),
            })
            .ToListAsync();

        return rows
            .Where(r => r.Classes.Count > 0)
            .Select(r => new NomGroup(r.Species, r.EntryType, r.DisplayName, r.Classes))
            .ToList();
    }

    // ── Edição de inscrição convoyage ─────────────────────────────────────────

    public record ConvoyageAveEdit(
        string Serie, string Especie, string TipoClasse,
        string EspecieMutacao, string Anilha,
        Guid? EquipaId, string? PosicaoEquipa);

    public record ConvoyageAveVendaEdit(
        string Especie, string? TipoClasse, string EspecieMutacao, bool EspecieLivre,
        string? DataNascimento, string Sexo, decimal Preco, string Anilha);

    public record ConvoyageAveTransporteEdit(
        string Especie,
        string Origem, string Anilha,
        string DestinatarioNome, string DestinatarioWhatsapp,
        string? DestinatarioNotas = null);

    public record ConvoyageEditModel(
        string NomeCompleto, string Email, string? Telefone, string Pais,
        string? NumeroStam, string LocalRecolha,
        bool SocioBva, string SocioBvaStatus,
        List<ConvoyageAveEdit> Aves,
        List<ConvoyageAveVendaEdit> AvesVenda,
        List<ConvoyageAveTransporteEdit> AvesTransporte,
        // Campos usados na Declaração TRACES (adicionados posteriormente — nullable
        // para submissões antigas onde o formulário ainda não os pedia).
        string? Morada = null,
        string? CodigoPostal = null,
        string? Localidade = null,
        string? AssinaturaPath = null,
        bool DeclaraArt59 = false);

    // Actualiza (ou cria) a assinatura de uma inscrição de convoyage a partir
    // de um dataURL PNG. Escreve o PNG em disco (mesmo caminho relativo do
    // submit) e regista AssinaturaPath no DataJson. Devolve erro se o formato
    // for inválido ou o ficheiro não puder ser escrito.
    public async Task<string?> UpdateConvoyageSignatureAsync(int id, string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return "Assinatura vazia.";
        const string prefix = "data:image/png;base64,";
        if (!dataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return "Formato de assinatura inválido (esperado PNG dataURL).";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[prefix.Length..]); }
        catch (FormatException) { return "Assinatura inválida (base64 mal formado)."; }
        if (bytes.Length < 200 || bytes.Length > 512 * 1024)
            return "Assinatura fora do tamanho aceite (200 B – 512 KB).";

        var f = await db.FormSubmissions
            .Include(x => x.Site)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return "Inscrição não encontrada.";
        if (f.FormType != FormType.InscricaoConvoyage) return "Inscrição não é de convoyage.";

        var storageRoot = ResolveStorageRoot();
        var subDate = f.SubmittedAt == default ? DateTime.UtcNow : f.SubmittedAt;
        var relDir = Path.Combine(f.Site.Slug, subDate.ToString("yyyy"), subDate.ToString("MM"));
        var absDir = Path.Combine(storageRoot, relDir);
        Directory.CreateDirectory(absDir);
        var fileName = $"assinatura-{f.Id}.png";
        var abs = Path.GetFullPath(Path.Combine(absDir, fileName));
        if (!abs.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase)) return "Caminho inválido.";
        try { await File.WriteAllBytesAsync(abs, bytes); }
        catch (Exception ex) { return $"Falha a gravar assinatura: {ex.Message}"; }

        var relPath = Path.Combine(relDir, fileName).Replace('\\', '/');

        // Actualiza o DataJson mantendo tudo o resto. Se o campo não existir
        // (submissão antiga), acrescenta-o.
        try
        {
            using var doc = JsonDocument.Parse(f.DataJson);
            var dict = new Dictionary<string, object?>();
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
            dict["AssinaturaPath"] = relPath;
            f.DataJson = JsonSerializer.Serialize(dict);
        }
        catch
        {
            // Fallback: se DataJson estiver corrompido, guarda um objecto mínimo.
            f.DataJson = JsonSerializer.Serialize(new { AssinaturaPath = relPath });
        }

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<ConvoyageEditModel?> GetConvoyageEditAsync(int id)
    {
        var f = await db.FormSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (f is null || f.FormType != FormType.InscricaoConvoyage) return null;
        return ParseEdit(f.DataJson);
    }

    public async Task<string?> UpdateConvoyageAsync(int id, ConvoyageEditModel model, bool resendEmails = false)
    {
        var f = await db.FormSubmissions
            .Include(x => x.BirdEntries)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return "Inscrição não encontrada.";
        if (f.FormType != FormType.InscricaoConvoyage) return "Inscrição não é do tipo convoyage.";

        if (string.IsNullOrWhiteSpace(model.NomeCompleto)) return "Nome completo obrigatório.";
        if (string.IsNullOrWhiteSpace(model.Email)) return "Email obrigatório.";
        if (string.IsNullOrWhiteSpace(model.Pais)) return "País obrigatório.";
        if (string.IsNullOrWhiteSpace(model.NumeroStam)) return "Nº STAM obrigatório.";
        var totalItens = (model.Aves?.Count ?? 0) + (model.AvesVenda?.Count ?? 0) + (model.AvesTransporte?.Count ?? 0);
        if (totalItens == 0) return "É necessário pelo menos uma ave (concurso, venda ou transporte).";
        foreach (var (a, i) in (model.Aves ?? new()).Select((a, i) => (a, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(a.Serie)) return $"Ave {i}: série obrigatória.";
            if (string.IsNullOrWhiteSpace(a.EspecieMutacao)) return $"Ave {i}: espécie/mutação obrigatória.";
            if (string.IsNullOrWhiteSpace(a.Anilha)) return $"Ave {i}: anilha obrigatória.";
        }
        foreach (var (a, i) in (model.AvesVenda ?? new()).Select((a, i) => (a, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(a.Especie)) return $"Ave venda {i}: espécie obrigatória.";
            if (string.IsNullOrWhiteSpace(a.EspecieMutacao)) return $"Ave venda {i}: mutação obrigatória.";
            if (string.IsNullOrWhiteSpace(a.Anilha)) return $"Ave venda {i}: anilha obrigatória.";
            if (a.Preco < 0) return $"Ave venda {i}: preço inválido.";
        }
        foreach (var (a, i) in (model.AvesTransporte ?? new()).Select((a, i) => (a, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(a.Origem)) return $"Ave transporte {i}: origem obrigatória (Compra ou Vende).";
            if (string.IsNullOrWhiteSpace(a.Especie)) return $"Ave transporte {i}: espécie obrigatória.";
            if (string.IsNullOrWhiteSpace(a.Anilha)) return $"Ave transporte {i}: anilha obrigatória.";
            if (string.IsNullOrWhiteSpace(a.DestinatarioNome)) return $"Ave transporte {i}: nome do destinatário obrigatório.";
            if (string.IsNullOrWhiteSpace(a.DestinatarioWhatsapp)) return $"Ave transporte {i}: WhatsApp do destinatário obrigatório.";
        }

        var existing = ParseEdit(f.DataJson) ?? new ConvoyageEditModel(
            "", "", null, "", null, "", false, "NaoSocio", new(), new(), new(),
            Morada: null, CodigoPostal: null, Localidade: null, AssinaturaPath: null, DeclaraArt59: false);

        // Preservar campos que não editamos aqui.
        int convoyageYear = 0;
        string? pdfPath = null;
        string? assinaturaPath = null;
        bool aceitouRegulamento = true;
        try
        {
            using var doc = JsonDocument.Parse(f.DataJson);
            var r = doc.RootElement;
            if (r.TryGetProperty("ConvoyageYear", out var y) && y.ValueKind == JsonValueKind.Number)
                convoyageYear = y.GetInt32();
            if (r.TryGetProperty("PdfPath", out var p) && p.ValueKind == JsonValueKind.String)
                pdfPath = p.GetString();
            if (r.TryGetProperty("AssinaturaPath", out var ap) && ap.ValueKind == JsonValueKind.String)
                assinaturaPath = ap.GetString();
            if (r.TryGetProperty("AceitouRegulamento", out var ar) &&
                (ar.ValueKind == JsonValueKind.True || ar.ValueKind == JsonValueKind.False))
                aceitouRegulamento = ar.GetBoolean();
        }
        catch { }

        // Recalcular custos com base nos dados novos.
        var aves = model.Aves ?? new();
        var vendaList = model.AvesVenda ?? new();
        var transporteList = model.AvesTransporte ?? new();
        var numAves = aves.Count;
        var numVenda = vendaList.Count;
        var numTransporte = transporteList.Count;
        var statusEnum = Enum.TryParse<SocioBvaStatus>(model.SocioBvaStatus, out var st)
            ? st
            : (model.SocioBva ? SocioBvaStatus.JaSocio : SocioBvaStatus.NaoSocio);
        var custos = ConvoyagePricing.Compute(numAves, numVenda, numTransporte, statusEnum);
        var tarifa = ConvoyagePricing.TransportePorAve(model.SocioBva);
        var tarifaAdq = ConvoyagePricing.TransporteAdquiridaPorAve(model.SocioBva);

        // Regenerar PDF com os dados actualizados.
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == f.SiteId);
        if (site is null) return "Site associado não encontrado.";

        ConvoyageYear? convYear = null;
        if (f.ConvoyageYearId is int cyId)
        {
            convYear = await db.ConvoyageYears.AsNoTracking()
                .Include(y => y.CollectionPoints)
                .FirstOrDefaultAsync(y => y.Id == cyId);
        }

        var point = convYear?.CollectionPoints
            .FirstOrDefault(p => p.Id == (f.LocalRecolhaId ?? 0));
        var localRecolhaLabel = point is not null
            ? $"{point.Name}{(point.Location is not null ? $" ({point.Location})" : "")}"
            : (model.LocalRecolha ?? "");

        byte[]? logoBytes = null;
        var logoFile = $"logo-{site.Slug}.png";
        string[] logoCandidates =
        {
            Path.Combine(AppContext.BaseDirectory, "PdfAssets", logoFile),
            Path.Combine(env.ContentRootPath, "PdfAssets", logoFile),
        };
        foreach (var p in logoCandidates)
        {
            if (!File.Exists(p)) continue;
            try { logoBytes = await File.ReadAllBytesAsync(p); break; }
            catch { /* logo é opcional */ }
        }

        var pdfReq = new InscricaoConvoyageRequest(
            NomeCompleto: model.NomeCompleto,
            Email: model.Email,
            Telefone: model.Telefone,
            Pais: model.Pais,
            NumeroStam: model.NumeroStam,
            LocalRecolhaId: f.LocalRecolhaId ?? 0,
            AceitouRegulamento: aceitouRegulamento,
            SocioBvaStatus: statusEnum,
            Aves: aves.Select(a => new AveConvoyageDto(
                a.Serie ?? "", a.EspecieMutacao ?? "", a.Especie ?? "",
                a.TipoClasse ?? "", a.Anilha ?? "",
                a.EquipaId, a.PosicaoEquipa)).ToList(),
            AvesVenda: vendaList.Select(a => new AveVendaDto(
                a.Especie ?? "", a.TipoClasse, a.EspecieMutacao ?? "",
                a.EspecieLivre, a.DataNascimento,
                ParseSexo(a.Sexo), a.Preco, a.Anilha ?? "")).ToList(),
            AvesTransporte: transporteList.Select(a => new AveTransporteDto(
                a.Especie ?? "",
                ParseOrigem(a.Origem),
                a.Anilha ?? "",
                a.DestinatarioNome ?? "",
                a.DestinatarioWhatsapp ?? "",
                a.DestinatarioNotas)).ToList(),
            TurnstileToken: null);

        byte[] pdfBytes;
        try
        {
            pdfBytes = InscricaoConvoyagePdfGenerator.Render(
                site, pdfReq, f.Id, localRecolhaLabel,
                convYear?.Year ?? convoyageYear, logoBytes);
        }
        catch (Exception ex)
        {
            return $"Falha a gerar o PDF: {ex.Message}";
        }

        var storageRoot = ResolveStorageRoot();
        string relPath;
        string absPath;
        if (!string.IsNullOrWhiteSpace(pdfPath))
        {
            relPath = pdfPath!.Replace('\\', '/');
            absPath = Path.GetFullPath(Path.Combine(storageRoot, relPath));
            if (!absPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase))
                return "Caminho de PDF inválido.";
        }
        else
        {
            var subDate = f.SubmittedAt == default ? DateTime.UtcNow : f.SubmittedAt;
            var relDir = Path.Combine(site.Slug, subDate.ToString("yyyy"), subDate.ToString("MM"));
            var pdfFileName = $"convoyage-{f.Id}.pdf";
            relPath = Path.Combine(relDir, pdfFileName).Replace('\\', '/');
            absPath = Path.GetFullPath(Path.Combine(storageRoot, relPath));
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
            await File.WriteAllBytesAsync(absPath, pdfBytes);
        }
        catch (Exception ex)
        {
            return $"Falha a gravar o PDF: {ex.Message}";
        }

        pdfPath = relPath;

        var stored = new
        {
            NomeCompleto = model.NomeCompleto,
            Email = model.Email,
            Telefone = model.Telefone,
            Pais = model.Pais,
            NumeroStam = model.NumeroStam,
            Morada = model.Morada,
            CodigoPostal = model.CodigoPostal,
            Localidade = model.Localidade,
            AssinaturaPath = assinaturaPath,
            DeclaraArt59 = model.DeclaraArt59,
            AceitouRegulamento = aceitouRegulamento,
            SocioBva = model.SocioBva,
            SocioBvaStatus = model.SocioBvaStatus,
            LocalRecolha = model.LocalRecolha,
            ConvoyageYear = convoyageYear,
            TotalAves = numAves,
            Aves = aves.Select(a => new
            {
                a.Serie, a.EspecieMutacao, a.Especie, a.TipoClasse, a.Anilha,
                a.EquipaId, a.PosicaoEquipa,
            }),
            TotalAvesVenda = numVenda,
            AvesVenda = vendaList.Select(a => new
            {
                a.Especie, a.TipoClasse, a.EspecieMutacao, a.EspecieLivre,
                a.DataNascimento, a.Sexo, a.Preco, a.Anilha
            }),
            TotalAvesTransporte = numTransporte,
            AvesTransporte = transporteList.Select(a => new
            {
                a.Especie,
                a.Origem, a.Anilha,
                a.DestinatarioNome, a.DestinatarioWhatsapp,
                a.DestinatarioNotas,
            }),
            Custos = new
            {
                fixa = custos.fixa,
                inscricoes = custos.inscricoes,
                gaiolas = custos.gaiolas,
                transporte = custos.transporte,
                transporteAdquiridas = custos.transporteAdquiridas,
                quota = custos.quota,
                total = custos.total,
                TarifaTransportePorAve = tarifa,
                TarifaTransporteAdquiridaPorAve = tarifaAdq,
            },
            PdfPath = pdfPath,
        };

        f.DataJson = JsonSerializer.Serialize(stored);

        // Reconstruir BirdEntries. Ligação à nomenclatura só é possível se houver
        // ano associado (submissão criada com ConvoyageYearId).
        db.ConvoyageBirdEntries.RemoveRange(f.BirdEntries);
        if (f.ConvoyageYearId is int yearId && aves.Count > 0)
        {
            var classes = await db.NomenclatureClasses
                .AsNoTracking()
                .Where(c => c.NomenclatureGroup.ConvoyageYearId == yearId && c.IsActive)
                .Select(c => new { c.Id, c.Code, c.Mutation })
                .ToListAsync();
            var lookup = classes.ToDictionary(c => c.Code + "|" + c.Mutation, c => c.Id);

            int order = 0;
            foreach (var ave in aves)
            {
                order++;
                var key = (ave.Serie ?? "").Trim() + "|" + (ave.EspecieMutacao ?? "").Trim();
                if (!lookup.TryGetValue(key, out var classId)) continue;
                db.ConvoyageBirdEntries.Add(new ConvoyageBirdEntry
                {
                    FormSubmissionId = f.Id,
                    BirdOrder = order,
                    NomenclatureClassId = classId,
                    RingNumber = (ave.Anilha ?? "").Trim(),
                    EquipaId = ave.EquipaId,
                    PosicaoEquipa = ave.PosicaoEquipa,
                });
            }
        }

        await db.SaveChangesAsync();

        if (resendEmails)
        {
            // Regenerar TRACES (se o ano tem Campeonato + Matrícula TRACES configurados
            // e existe assinatura persistida — mesma regra do submit público).
            byte[]? tracesBytes = null;
            try
            {
                var traces = await TracesPdfBuilder.BuildAsync(db, env, config, f.Id, site.Slug);
                tracesBytes = traces?.Bytes;
            }
            catch { /* TRACES é opcional; falhar não deve bloquear o reenvio. */ }

            var pdfAttachment = new EmailSender.EmailAttachment(
                FileName: SafeFileName($"convoyage-{model.NomeCompleto}.pdf"),
                Content: pdfBytes,
                ContentType: "application/pdf");
            var attachments = new List<EmailSender.EmailAttachment> { pdfAttachment };
            if (tracesBytes is not null)
            {
                attachments.Add(new EmailSender.EmailAttachment(
                    FileName: SafeFileName($"TRACES-{model.NomeCompleto}.pdf"),
                    Content: tracesBytes,
                    ContentType: "application/pdf"));
            }
            var attachmentsArray = attachments.ToArray();

            var yearForEmail = convYear?.Year ?? convoyageYear;

            if (!string.IsNullOrWhiteSpace(site.ContactEmail))
            {
                try
                {
                    var (fromEmailA, fromNameA) = site.MailFrom();
                    await email.SendAsync(
                        site.ContactEmail,
                        $"[{site.Name}] Inscrição convoyage actualizada — {model.NomeCompleto}",
                        ConvoyageEmailRenderer.RenderAssociacao(site, pdfReq, f.Id, localRecolhaLabel, yearForEmail),
                        attachmentsArray,
                        replyTo: model.Email,
                        fromEmail: fromEmailA,
                        fromName: fromNameA);
                }
                catch (Exception ex)
                {
                    return $"Alterações guardadas, mas falhou o envio do email à associação: {ex.Message}";
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                try
                {
                    var (fromEmailC, fromNameC) = site.MailFrom();
                    await email.SendAsync(
                        model.Email,
                        $"[{site.Name}] Inscrição convoyage actualizada",
                        ConvoyageEmailRenderer.RenderCriador(site, pdfReq, f.Id, localRecolhaLabel, yearForEmail),
                        attachmentsArray,
                        replyTo: site.ContactEmail,
                        fromEmail: fromEmailC,
                        fromName: fromNameC);
                }
                catch (Exception ex)
                {
                    return $"Alterações guardadas, mas falhou o envio do email ao criador: {ex.Message}";
                }
            }
        }

        return null;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return clean.Length > 100 ? clean[..100] : clean;
    }

    private static ConvoyageEditModel? ParseEdit(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            string? S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            bool B(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

            var aves = new List<ConvoyageAveEdit>();
            if (r.TryGetProperty("Aves", out var avesEl) && avesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in avesEl.EnumerateArray())
                {
                    string AS(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                    Guid? AG(string k)
                    {
                        if (!a.TryGetProperty(k, out var v)) return null;
                        if (v.ValueKind != JsonValueKind.String) return null;
                        return Guid.TryParse(v.GetString(), out var g) ? g : (Guid?)null;
                    }
                    aves.Add(new ConvoyageAveEdit(
                        AS("Serie"), AS("Especie"), AS("TipoClasse"),
                        AS("EspecieMutacao"), AS("Anilha"),
                        AG("EquipaId"), AS("PosicaoEquipa")));
                }
            }

            var venda = new List<ConvoyageAveVendaEdit>();
            if (r.TryGetProperty("AvesVenda", out var vEl) && vEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in vEl.EnumerateArray())
                {
                    string AS(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                    bool AB(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
                    decimal AD(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;
                    venda.Add(new ConvoyageAveVendaEdit(
                        AS("Especie"), AS("TipoClasse"), AS("EspecieMutacao"), AB("EspecieLivre"),
                        AS("DataNascimento"), AS("Sexo"), AD("Preco"), AS("Anilha")));
                }
            }

            var transporte = new List<ConvoyageAveTransporteEdit>();
            if (r.TryGetProperty("AvesTransporte", out var tEl) && tEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in tEl.EnumerateArray())
                {
                    string AS(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                    string? ASn(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    transporte.Add(new ConvoyageAveTransporteEdit(
                        AS("Especie"),
                        AS("Origem"), AS("Anilha"),
                        AS("DestinatarioNome"), AS("DestinatarioWhatsapp"),
                        ASn("DestinatarioNotas")));
                }
            }

            return new ConvoyageEditModel(
                S("NomeCompleto") ?? "", S("Email") ?? "", S("Telefone"), S("Pais") ?? "",
                S("NumeroStam"), S("LocalRecolha") ?? "",
                B("SocioBva"), S("SocioBvaStatus") ?? "NaoSocio",
                aves, venda, transporte,
                Morada: S("Morada"),
                CodigoPostal: S("CodigoPostal"),
                Localidade: S("Localidade"),
                AssinaturaPath: S("AssinaturaPath"),
                DeclaraArt59: B("DeclaraArt59"));
        }
        catch { return null; }
    }

    private static SexoAve ParseSexo(string? raw) => raw switch
    {
        "Macho" or "M" => SexoAve.Macho,
        "Femea" or "Fêmea" or "F" => SexoAve.Femea,
        _ => SexoAve.Indefinido,
    };

    private static OrigemAveTransporte ParseOrigem(string? raw) => raw switch
    {
        "Compra" or "Adquirida" => OrigemAveTransporte.Compra,
        "Vende" or "Cedida" => OrigemAveTransporte.Vende,
        _ => OrigemAveTransporte.Compra,
    };
}
