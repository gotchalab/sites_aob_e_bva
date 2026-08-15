using System.Text.Json;
using AOB.Application.Contracts;
using AOB.Application.Forms;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AOB.Api.Endpoints;

public static class FormEndpoints
{
    public static RouteGroupBuilder MapForms(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sites/{siteSlug}/forms").WithTags("Forms");

        g.MapPost("/contact", SubmitContact);
        g.MapPost("/inscricao-socio", SubmitInscricaoSocio);
        g.MapPost("/inscricao-convoyage", SubmitInscricaoConvoyage);
        return g;
    }

    private static async Task<Results<Ok<FormSubmissionResponse>, BadRequest<FormSubmissionResponse>, NotFound>>
        SubmitContact(
            string siteSlug,
            [FromBody] ContactRequest req,
            AppDbContext db,
            TurnstileVerifier turnstile,
            EmailSender email,
            HttpContext http,
            CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == siteSlug, ct);
        if (site is null) return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Message))
        {
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Campos obrigatorios em falta."));
        }

        var ip = http.Connection.RemoteIpAddress?.ToString();
        if (!await turnstile.VerifyAsync(req.TurnstileToken, ip, ct))
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Verificacao anti-bot falhou."));

        var submission = new FormSubmission
        {
            SiteId = site.Id,
            FormType = FormType.Contact,
            DataJson = JsonSerializer.Serialize(new { req.Name, req.Email, req.Phone, req.Subject, req.Message }),
            IpAddress = ip,
            UserAgent = http.Request.Headers.UserAgent.ToString(),
        };
        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(site.ContactEmail))
        {
            try
            {
                await email.SendAsync(site.ContactEmail,
                    $"[{site.Name}] Contacto: {req.Subject}",
                    RenderContactEmail(req),
                    attachments: null,
                    fromOverride: site.ContactEmail,
                    ct);
            }
            catch { /* nao falhar a submissao por causa do email */ }
        }

        return TypedResults.Ok(new FormSubmissionResponse(true, SubmissionId: submission.Id));
    }

    private static async Task<Results<Ok<FormSubmissionResponse>, BadRequest<FormSubmissionResponse>, NotFound>>
        SubmitInscricaoSocio(
            string siteSlug,
            AppDbContext db,
            TurnstileVerifier turnstile,
            EmailSender email,
            IConfiguration config,
            IHostEnvironment env,
            ILoggerFactory logFactory,
            HttpContext http,
            CancellationToken ct)
    {
        var log = logFactory.CreateLogger("InscricaoSocio");

        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == siteSlug, ct);
        if (site is null) return TypedResults.NotFound();

        if (!http.Request.HasFormContentType)
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Formato invalido — envie multipart/form-data."));

        var form = await http.Request.ReadFormAsync(ct);
        var req = BindInscricaoRequest(form);
        var foto = form.Files.GetFile("foto");
        var assinatura = form.Files.GetFile("assinatura");
        var comprovativo = form.Files.GetFile("comprovativoPagamento");

        var validationError = ValidateInscricao(req, assinatura, comprovativo);
        if (validationError is not null)
            return TypedResults.BadRequest(new FormSubmissionResponse(false, validationError));

        var ip = http.Connection.RemoteIpAddress?.ToString();
        if (!await turnstile.VerifyAsync(req.TurnstileToken, ip, ct))
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Verificacao anti-bot falhou."));

        // Limites de tamanho (bytes)
        if ((foto?.Length ?? 0) > 5_000_000
            || (assinatura?.Length ?? 0) > 1_500_000
            || (comprovativo?.Length ?? 0) > 8_000_000)
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Anexos demasiado grandes."));

        var fotoBytes = await ReadAllBytes(foto, ct);
        var assinaturaBytes = await ReadAllBytes(assinatura, ct);
        var comprovativoBytes = await ReadAllBytes(comprovativo, ct) ?? Array.Empty<byte>();
        var comprovativoContentType = string.IsNullOrWhiteSpace(comprovativo?.ContentType)
            ? "application/octet-stream" : comprovativo!.ContentType;
        var comprovativoExt = ExtensionFromContentTypeOrName(comprovativo?.ContentType, comprovativo?.FileName);

        // 1) guardar preliminar para obter Id
        var submission = new FormSubmission
        {
            SiteId = site.Id,
            FormType = FormType.InscricaoSocio,
            DataJson = "{}",
            IpAddress = ip,
            UserAgent = http.Request.Headers.UserAgent.ToString(),
        };
        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync(ct);

        // 2) gerar PDF
        byte[] pdfBytes;
        try
        {
            pdfBytes = InscricaoSocioPdfGenerator.Render(site, req, submission.Id, fotoBytes, assinaturaBytes);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha a gerar PDF da inscricao #{Id}", submission.Id);
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Falha a gerar o PDF."));
        }

        // 3) guardar PDF em path privado
        var storageRoot = config["Inscricoes:StorageRoot"];
        if (string.IsNullOrWhiteSpace(storageRoot))
            storageRoot = Path.Combine(env.ContentRootPath, "PrivateData", "inscricoes");
        var relDir = Path.Combine(siteSlug, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var absDir = Path.Combine(storageRoot, relDir);
        Directory.CreateDirectory(absDir);
        var pdfFileName = $"inscricao-{submission.Id}.pdf";
        var absPath = Path.Combine(absDir, pdfFileName);
        await File.WriteAllBytesAsync(absPath, pdfBytes, ct);
        var relPath = Path.Combine(relDir, pdfFileName).Replace('\\', '/');

        // 3b) guardar comprovativo de pagamento (obrigatorio; ja validado acima)
        var comprovativoFileName = $"comprovativo-{submission.Id}{comprovativoExt}";
        var comprovativoAbsPath = Path.Combine(absDir, comprovativoFileName);
        await File.WriteAllBytesAsync(comprovativoAbsPath, comprovativoBytes, ct);
        var comprovativoRelPath = Path.Combine(relDir, comprovativoFileName).Replace('\\', '/');

        // 4) actualizar DataJson (meta + paths; sem bytes)
        var stored = new
        {
            req.NomeCompleto, req.Email, req.Telefone, req.CartaoCidadao, req.NIF,
            req.Nacionalidade, req.DataNascimento, EstadoCivil = req.EstadoCivil?.ToString(),
            req.Morada, req.MoradaLinha2, req.CodigoPostal, req.Localidade, req.Profissao,
            req.SocioApoiante, req.SocioCriador,
            StamFonp = req.StamFonp?.ToString(), req.StamFonpNumero,
            req.SocioBvaPortugal, StamBva = req.StamBva?.ToString(), req.StamBvaNumero,
            req.AceitouRegulamento,
            HasFoto = fotoBytes is not null && fotoBytes.Length > 0,
            HasAssinatura = assinaturaBytes is not null && assinaturaBytes.Length > 0,
            req.Notas,
            PdfPath = relPath,
            ComprovativoPath = comprovativoRelPath,
            ComprovativoNome = comprovativo?.FileName,
        };
        submission.DataJson = JsonSerializer.Serialize(stored);
        await db.SaveChangesAsync(ct);

        // 5) email a associacao (com PDF + comprovativo anexos)
        var pdfAttachment = new EmailSender.EmailAttachment(
            FileName: SafeFileName($"inscricao-socio-{req.NomeCompleto}.pdf"),
            Content: pdfBytes,
            ContentType: "application/pdf");
        var comprovativoAttachment = new EmailSender.EmailAttachment(
            FileName: SafeFileName($"comprovativo-{req.NomeCompleto}{comprovativoExt}"),
            Content: comprovativoBytes,
            ContentType: comprovativoContentType!);

        if (!string.IsNullOrWhiteSpace(site.ContactEmail))
        {
            try
            {
                await email.SendAsync(
                    site.ContactEmail,
                    $"[{site.Name}] Novo pedido de inscricao — {req.NomeCompleto}",
                    RenderInscricaoEmailAssociacao(site, req, submission.Id),
                    new[] { pdfAttachment, comprovativoAttachment },
                    fromOverride: site.ContactEmail,
                    ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Falha a enviar email a associacao para inscricao #{Id}", submission.Id);
            }
        }

        // 6) email de confirmacao ao candidato (com PDF anexo)
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            try
            {
                await email.SendAsync(
                    req.Email,
                    $"[{site.Name}] Recebemos o teu pedido de inscricao",
                    RenderInscricaoEmailCandidato(site, req),
                    new[] { pdfAttachment },
                    fromOverride: site.ContactEmail,
                    ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Falha a enviar email de confirmacao ao candidato para inscricao #{Id}", submission.Id);
            }
        }

        return TypedResults.Ok(new FormSubmissionResponse(true, SubmissionId: submission.Id));
    }

    private static string? ValidateInscricao(InscricaoSocioRequest r, IFormFile? assinatura, IFormFile? comprovativo)
    {
        if (string.IsNullOrWhiteSpace(r.NomeCompleto)) return "Nome completo obrigatorio.";
        if (string.IsNullOrWhiteSpace(r.Email)) return "Email obrigatorio.";
        if (string.IsNullOrWhiteSpace(r.CartaoCidadao)) return "Cartao de Cidadao/BI obrigatorio.";
        if (string.IsNullOrWhiteSpace(r.NIF)) return "NIF obrigatorio.";
        if (string.IsNullOrWhiteSpace(r.Morada)) return "Morada obrigatoria.";
        if (string.IsNullOrWhiteSpace(r.CodigoPostal)) return "Codigo postal obrigatorio.";
        if (!r.AceitouRegulamento) return "E necessario aceitar o Regulamento Geral Interno.";
        if (assinatura is null || assinatura.Length == 0) return "A assinatura e obrigatoria.";
        if (comprovativo is null || comprovativo.Length == 0) return "O comprovativo de pagamento e obrigatorio.";
        return null;
    }

    private static InscricaoSocioRequest BindInscricaoRequest(IFormCollection f)
    {
        static string? S(IFormCollection f, string k)
        {
            var v = f[k].ToString();
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }
        static bool B(IFormCollection f, string k)
        {
            var v = f[k].ToString();
            return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1" || v.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
        static DateTime? D(IFormCollection f, string k)
        {
            var v = S(f, k);
            if (v is null) return null;
            return DateTime.TryParse(v, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dt) ? dt : (DateTime?)null;
        }
        static T? E<T>(IFormCollection f, string k) where T : struct
        {
            var v = S(f, k);
            if (v is null) return null;
            return Enum.TryParse<T>(v, ignoreCase: true, out var e) ? e : (T?)null;
        }

        return new InscricaoSocioRequest(
            NomeCompleto: S(f, "nomeCompleto") ?? "",
            Email: S(f, "email") ?? "",
            Telefone: S(f, "telefone"),
            CartaoCidadao: S(f, "cartaoCidadao"),
            NIF: S(f, "nif"),
            Nacionalidade: S(f, "nacionalidade"),
            DataNascimento: D(f, "dataNascimento"),
            EstadoCivil: E<EstadoCivilOpt>(f, "estadoCivil"),
            Morada: S(f, "morada"),
            MoradaLinha2: S(f, "moradaLinha2"),
            CodigoPostal: S(f, "codigoPostal"),
            Localidade: S(f, "localidade"),
            Profissao: S(f, "profissao"),
            SocioApoiante: B(f, "socioApoiante"),
            SocioCriador: B(f, "socioCriador"),
            StamFonp: E<StamStatus>(f, "stamFonp"),
            StamFonpNumero: S(f, "stamFonpNumero"),
            SocioBvaPortugal: B(f, "socioBvaPortugal"),
            StamBva: E<StamStatus>(f, "stamBva"),
            StamBvaNumero: S(f, "stamBvaNumero"),
            AceitouRegulamento: B(f, "aceitouRegulamento"),
            Notas: S(f, "notas"),
            TurnstileToken: S(f, "turnstileToken"));
    }

    private static async Task<byte[]?> ReadAllBytes(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        using var ms = new MemoryStream();
        await using var s = file.OpenReadStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static string ExtensionFromContentTypeOrName(string? contentType, string? fileName)
    {
        var ext = string.IsNullOrWhiteSpace(fileName) ? "" : Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(ext)) return ext.ToLowerInvariant();
        return (contentType ?? "").ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".bin",
        };
    }

    private static string RenderContactEmail(ContactRequest r) => $"""
        <h3>Contacto recebido</h3>
        <p><b>Nome:</b> {H(r.Name)}<br />
           <b>Email:</b> {H(r.Email)}<br />
           <b>Telefone:</b> {H(r.Phone)}<br />
           <b>Assunto:</b> {H(r.Subject)}</p>
        <p><b>Mensagem:</b></p>
        <div style="white-space: pre-wrap; border-left: 3px solid #ccc; padding-left: 12px;">{H(r.Message)}</div>
        """;

    private static string RenderInscricaoEmailAssociacao(Site site, InscricaoSocioRequest r, int submissionId) => $"""
        <h2 style="color:#1a4380;margin:0 0 8px">Novo pedido de inscricao de socio</h2>
        <p style="color:#666;margin:0 0 16px">Submissao #{submissionId} · {site.Name}</p>

        <h3>Dados pessoais</h3>
        <table cellpadding="4" style="border-collapse:collapse;font-size:13px">
          <tr><td><b>Nome:</b></td><td>{H(r.NomeCompleto)}</td></tr>
          <tr><td><b>CC/BI:</b></td><td>{H(r.CartaoCidadao)}</td></tr>
          <tr><td><b>NIF:</b></td><td>{H(r.NIF)}</td></tr>
          <tr><td><b>Nacionalidade:</b></td><td>{H(r.Nacionalidade)}</td></tr>
          <tr><td><b>Data nascimento:</b></td><td>{r.DataNascimento?.ToString("yyyy-MM-dd")}</td></tr>
          <tr><td><b>Estado civil:</b></td><td>{r.EstadoCivil}</td></tr>
          <tr><td><b>Email:</b></td><td>{H(r.Email)}</td></tr>
          <tr><td><b>Telefone:</b></td><td>{H(r.Telefone)}</td></tr>
          <tr><td><b>Morada:</b></td><td>{H(r.Morada)} {H(r.MoradaLinha2)}<br/>{H(r.CodigoPostal)} {H(r.Localidade)}</td></tr>
          <tr><td><b>Profissao:</b></td><td>{H(r.Profissao)}</td></tr>
        </table>

        <h3>Tipo de socio</h3>
        <ul>
          <li>Socio Apoiante: <b>{(r.SocioApoiante ? "Sim" : "Nao")}</b></li>
          <li>Socio Criador: <b>{(r.SocioCriador ? "Sim" : "Nao")}</b></li>
          <li>STAM FONP: <b>{r.StamFonp} {H(r.StamFonpNumero)}</b></li>
          <li>Socio BVA Portugal: <b>{(r.SocioBvaPortugal ? "Sim" : "Nao")}</b></li>
          <li>STAM BVA: <b>{r.StamBva} {H(r.StamBvaNumero)}</b></li>
        </ul>

        {(string.IsNullOrWhiteSpace(r.Notas) ? "" : $"<h3>Notas</h3><p>{H(r.Notas)}</p>")}

        <p style="margin-top:20px;color:#666;font-size:12px">O PDF com a ficha completa esta anexo a este email. O pedido esta pendente de aprovacao no backoffice.</p>
        """;

    private static string RenderInscricaoEmailCandidato(Site site, InscricaoSocioRequest r) => $"""
        <p>Ola {H(r.NomeCompleto)},</p>
        <p>Recebemos o teu pedido de inscricao como socio da <b>{H(site.Name)}</b>. Vai anexo o PDF com todos os dados que submeteste.</p>
        <p>A tua candidatura sera analisada em reuniao de Direcao. Sera contactado(a) por email assim que houver uma decisao.</p>
        <p style="margin-top:20px">Cumprimentos,<br/>{H(site.Name)}</p>
        <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
        <p style="color:#888;font-size:12px">Este e um email automatico. Se nao foste tu a submeter este pedido, por favor ignora ou responde a este email.</p>
        """;

    private static async Task<Results<Ok<FormSubmissionResponse>, BadRequest<FormSubmissionResponse>, NotFound>>
        SubmitInscricaoConvoyage(
            string siteSlug,
            [FromBody] InscricaoConvoyageRequest req,
            AppDbContext db,
            TurnstileVerifier turnstile,
            EmailSender email,
            IConfiguration config,
            IHostEnvironment env,
            ILoggerFactory logFactory,
            HttpContext http,
            CancellationToken ct)
    {
        var log = logFactory.CreateLogger("InscricaoConvoyage");

        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == siteSlug, ct);
        if (site is null) return TypedResults.NotFound();

        var activeYear = await db.ConvoyageYears
            .Include(y => y.CollectionPoints)
            .FirstOrDefaultAsync(y => y.SiteId == site.Id && y.IsActive, ct);
        if (activeYear is null)
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Nao ha inscricoes abertas para convoyage neste momento."));

        if (activeYear.RegistrationClosesAt.HasValue && DateTime.UtcNow >= activeYear.RegistrationClosesAt.Value)
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "As inscrições para a convoyage foram encerradas."));

        var collectionPoint = activeYear.CollectionPoints.FirstOrDefault(p => p.Id == req.LocalRecolhaId);
        if (collectionPoint is null)
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Local de recolha invalido."));

        var localRecolhaLabel = $"{collectionPoint.Name}{(collectionPoint.Location is not null ? $" ({collectionPoint.Location})" : "")}";

        var validationError = ValidateConvoyage(req);
        if (validationError is not null)
            return TypedResults.BadRequest(new FormSubmissionResponse(false, validationError));

        var ip = http.Connection.RemoteIpAddress?.ToString();
        if (!await turnstile.VerifyAsync(req.TurnstileToken, ip, ct))
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Verificacao anti-bot falhou."));

        var submission = new FormSubmission
        {
            SiteId = site.Id,
            FormType = FormType.InscricaoConvoyage,
            ConvoyageYearId = activeYear.Id,
            LocalRecolhaId = collectionPoint.Id,
            DataJson = "{}",
            IpAddress = ip,
            UserAgent = http.Request.Headers.UserAgent.ToString(),
        };
        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync(ct);

        byte[]? logoBytes = null;
        var logoPath = Path.Combine(env.ContentRootPath, "PdfAssets", $"logo-{siteSlug}.png");
        if (File.Exists(logoPath))
        {
            try { logoBytes = await File.ReadAllBytesAsync(logoPath, ct); }
            catch (Exception ex) { log.LogWarning(ex, "Falha a carregar logo {Path}", logoPath); }
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = InscricaoConvoyagePdfGenerator.Render(site, req, submission.Id, localRecolhaLabel, activeYear.Year, logoBytes);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha a gerar PDF convoyage #{Id}", submission.Id);
            return TypedResults.BadRequest(new FormSubmissionResponse(false, "Falha a gerar o PDF."));
        }

        var storageRoot = config["Inscricoes:StorageRoot"];
        if (string.IsNullOrWhiteSpace(storageRoot))
            storageRoot = Path.Combine(env.ContentRootPath, "PrivateData", "inscricoes");
        var relDir = Path.Combine(siteSlug, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var absDir = Path.Combine(storageRoot, relDir);
        Directory.CreateDirectory(absDir);
        var pdfFileName = $"convoyage-{submission.Id}.pdf";
        var absPath = Path.Combine(absDir, pdfFileName);
        await File.WriteAllBytesAsync(absPath, pdfBytes, ct);
        var relPath = Path.Combine(relDir, pdfFileName).Replace('\\', '/');

        var aves = req.Aves ?? new List<AveConvoyageDto>();
        var avesVenda = req.AvesVenda ?? new List<AveVendaDto>();
        var avesTransporte = req.AvesTransporte ?? new List<AveTransporteDto>();
        var custos = ConvoyagePricing.Compute(aves.Count, avesVenda.Count, avesTransporte.Count, req.SocioBvaStatus);
        var stored = new
        {
            req.NomeCompleto, req.Email, req.Telefone, req.Pais,
            req.NumeroStam, req.AceitouRegulamento,
            req.SocioBva,
            SocioBvaStatus = req.SocioBvaStatus.ToString(),
            LocalRecolha = localRecolhaLabel,
            ConvoyageYear = activeYear.Year,
            TotalAves = aves.Count,
            Aves = aves.Select(a => new
            {
                a.Serie, a.EspecieMutacao, a.Especie, a.TipoClasse, a.Anilha,
                a.EquipaId, a.PosicaoEquipa,
            }),
            TotalAvesVenda = avesVenda.Count,
            AvesVenda = avesVenda.Select(a => new
            {
                a.Especie, a.TipoClasse, a.EspecieMutacao, a.EspecieLivre,
                a.DataNascimento,
                Sexo = a.Sexo.ToString(),
                a.Preco,
                a.Anilha
            }),
            TotalAvesTransporte = avesTransporte.Count,
            AvesTransporte = avesTransporte.Select(a => new
            {
                a.Especie,
                Origem = a.Origem.ToString(),
                a.Anilha,
                a.DestinatarioNome,
                a.DestinatarioWhatsapp,
                a.DestinatarioNotas,
            }),
            Custos = new
            {
                custos.fixa,
                custos.inscricoes,
                custos.gaiolas,
                custos.transporte,
                custos.transporteAdquiridas,
                custos.quota,
                custos.total,
                TarifaTransportePorAve = ConvoyagePricing.TransportePorAve(req.SocioBva),
                TarifaTransporteAdquiridaPorAve = ConvoyagePricing.TransporteAdquiridaPorAve(req.SocioBva),
            },
            PdfPath = relPath,
        };
        submission.DataJson = JsonSerializer.Serialize(stored);

        // Persist structured bird entries for reporting/lookup. If a bird's
        // Serie/EspecieMutacao doesn't match any active class in the year's
        // nomenclature we skip it — DataJson still holds the raw snapshot.
        if (aves.Count > 0)
        {
            var classesByKey = await db.NomenclatureClasses
                .AsNoTracking()
                .Where(c => c.NomenclatureGroup.ConvoyageYearId == activeYear.Id && c.IsActive)
                .Select(c => new { c.Id, c.Code, c.Mutation })
                .ToListAsync(ct);
            var lookup = classesByKey.ToDictionary(
                c => c.Code + "|" + c.Mutation,
                c => c.Id);

            int order = 0;
            foreach (var ave in aves)
            {
                order++;
                var key = (ave.Serie ?? "").Trim() + "|" + (ave.EspecieMutacao ?? "").Trim();
                if (!lookup.TryGetValue(key, out var classId))
                {
                    log.LogWarning(
                        "Convoyage #{Id} ave #{Order}: sem match na nomenclatura para '{Serie}' / '{Mut}'",
                        submission.Id, order, ave.Serie, ave.EspecieMutacao);
                    continue;
                }
                db.ConvoyageBirdEntries.Add(new ConvoyageBirdEntry
                {
                    FormSubmissionId = submission.Id,
                    BirdOrder = order,
                    NomenclatureClassId = classId,
                    RingNumber = (ave.Anilha ?? "").Trim(),
                    EquipaId = ave.EquipaId,
                    PosicaoEquipa = ave.PosicaoEquipa,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var pdfAttachment = new EmailSender.EmailAttachment(
            FileName: SafeFileName($"convoyage-{req.NomeCompleto}.pdf"),
            Content: pdfBytes,
            ContentType: "application/pdf");

        if (!string.IsNullOrWhiteSpace(site.ContactEmail))
        {
            try
            {
                await email.SendAsync(
                    site.ContactEmail,
                    $"[{site.Name}] Nova inscrição convoyage — {req.NomeCompleto}",
                    RenderConvoyageEmailAssociacao(site, req, submission.Id, localRecolhaLabel, activeYear.Year),
                    new[] { pdfAttachment },
                    fromOverride: site.ContactEmail,
                    ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Falha a enviar email a associacao para convoyage #{Id}", submission.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            try
            {
                await email.SendAsync(
                    req.Email,
                    $"[{site.Name}] Recebemos a tua inscrição na convoyage",
                    RenderConvoyageEmailCriador(site, req, submission.Id, localRecolhaLabel, activeYear.Year),
                    new[] { pdfAttachment },
                    fromOverride: site.ContactEmail,
                    ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Falha a enviar email ao criador para convoyage #{Id}", submission.Id);
            }
        }

        return TypedResults.Ok(new FormSubmissionResponse(true, SubmissionId: submission.Id));
    }

    private static string? ValidateConvoyage(InscricaoConvoyageRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.NomeCompleto)) return "Nome completo obrigatorio.";
        if (string.IsNullOrWhiteSpace(r.Email)) return "Email obrigatorio.";
        if (string.IsNullOrWhiteSpace(r.Pais)) return "Pais obrigatorio.";
        if (string.IsNullOrWhiteSpace(r.NumeroStam)) return "Numero STAM obrigatorio.";
        if (!r.AceitouRegulamento) return "E necessario aceitar o regulamento.";
        var totalAvesEnviadas = (r.Aves?.Count ?? 0) + (r.AvesVenda?.Count ?? 0) + (r.AvesTransporte?.Count ?? 0);
        if (totalAvesEnviadas == 0) return "E necessario inscrever pelo menos uma ave (concurso, venda ou transporte).";
        foreach (var (ave, i) in (r.Aves ?? new List<AveConvoyageDto>()).Select((a, i) => (a, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(ave.Serie)) return $"Ave {i}: codigo de serie obrigatorio.";
            if (string.IsNullOrWhiteSpace(ave.EspecieMutacao)) return $"Ave {i}: especie/mutacao obrigatoria.";
            if (string.IsNullOrWhiteSpace(ave.Anilha)) return $"Ave {i}: numero de anilha obrigatorio.";
        }

        // Team validation: birds that share EquipaId must be exactly 4, with
        // distinct positions A/B/C/D and identical species/type/serie/mutation.
        var teams = (r.Aves ?? new List<AveConvoyageDto>())
            .Where(a => a.EquipaId is not null)
            .GroupBy(a => a.EquipaId!.Value);
        foreach (var team in teams)
        {
            var members = team.ToList();
            if (members.Count != 4)
                return $"Equipa {team.Key}: uma equipa tem de ter exactamente 4 aves (tem {members.Count}).";
            var positions = members
                .Select(m => (m.PosicaoEquipa ?? "").ToUpperInvariant())
                .OrderBy(p => p)
                .ToList();
            var expected = new[] { "A", "B", "C", "D" };
            if (!positions.SequenceEqual(expected))
                return $"Equipa {team.Key}: posicoes devem ser A, B, C, D (recebido: {string.Join(",", positions)}).";
            var first = members[0];
            if (members.Any(m =>
                m.Especie != first.Especie ||
                m.TipoClasse != first.TipoClasse ||
                m.Serie != first.Serie ||
                m.EspecieMutacao != first.EspecieMutacao))
                return $"Equipa {team.Key}: todas as aves devem partilhar especie, tipo, serie e mutacao.";
            if (members.Any(m => m.TipoClasse != "Team"))
                return $"Equipa {team.Key}: todas as aves devem ter tipo 'Team'.";
        }
        if (r.AvesVenda is { Count: > 0 })
        {
            foreach (var (ave, i) in r.AvesVenda.Select((a, i) => (a, i + 1)))
            {
                if (string.IsNullOrWhiteSpace(ave.Especie)) return $"Ave de venda {i}: especie obrigatoria.";
                if (string.IsNullOrWhiteSpace(ave.EspecieMutacao)) return $"Ave de venda {i}: especie/mutacao obrigatoria.";
                if (string.IsNullOrWhiteSpace(ave.Anilha)) return $"Ave de venda {i}: numero de anilha obrigatorio.";
                if (string.IsNullOrWhiteSpace(ave.DataNascimento)) return $"Ave de venda {i}: data de nascimento obrigatoria.";
                if (ave.Preco < 0) return $"Ave de venda {i}: preco invalido.";
            }
        }
        if (r.AvesTransporte is { Count: > 0 })
        {
            foreach (var (ave, i) in r.AvesTransporte.Select((a, i) => (a, i + 1)))
            {
                if (string.IsNullOrWhiteSpace(ave.Especie)) return $"Ave de transporte {i}: especie obrigatoria.";
                if (string.IsNullOrWhiteSpace(ave.Anilha)) return $"Ave de transporte {i}: numero de anilha obrigatorio.";
                if (string.IsNullOrWhiteSpace(ave.DestinatarioNome)) return $"Ave de transporte {i}: nome do destinatario obrigatorio.";
                if (string.IsNullOrWhiteSpace(ave.DestinatarioWhatsapp)) return $"Ave de transporte {i}: WhatsApp do destinatario obrigatorio.";
            }
        }
        return null;
    }

    private static string RenderConvoyageEmailAssociacao(Site site, InscricaoConvoyageRequest r, int submissionId, string localRecolha, int year)
    {
        var header = $"""
        <h2 style="color:#1a4380;margin:0 0 8px">Nova inscrição de convoyage {year}</h2>
        <p style="color:#666;margin:0 0 16px">Submissão #{submissionId} · {H(site.Name)}</p>
        """;
        return header + RenderConvoyageBody(site, r, localRecolha, year, forCriador: false);
    }

    private static string RenderConvoyageEmailCriador(Site site, InscricaoConvoyageRequest r, int submissionId, string localRecolha, int year)
    {
        var header = $"""
        <p>Olá {H(r.NomeCompleto)},</p>
        <p>Recebemos a tua inscrição na convoyage {year} da <b>{H(site.Name)}</b>. Segue em baixo o resumo dos dados que submeteste; o detalhe completo das aves e dos custos vai em anexo (PDF).</p>
        <p style="color:#666;margin:0 0 16px;font-size:12px">Referência: submissão #{submissionId}</p>
        """;
        var footer = $"""
        <p style="margin-top:20px">Cumprimentos,<br/>{H(site.Name)}</p>
        <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
        <p style="color:#888;font-size:12px">Este é um email automático. Se não foste tu a submeter esta inscrição, por favor contacta-nos.</p>
        """;
        return header + RenderConvoyageBody(site, r, localRecolha, year, forCriador: true) + footer;
    }

    private static string RenderConvoyageBody(Site site, InscricaoConvoyageRequest r, string localRecolha, int year, bool forCriador)
    {
        var totalAves = r.Aves?.Count ?? 0;
        var totalVenda = r.AvesVenda?.Count ?? 0;
        var totalTransporte = r.AvesTransporte?.Count ?? 0;

        var dadosHeader = forCriador ? "Dados submetidos" : "Dados do criador";

        var pagamento = forCriador
            ? "<div style=\"margin-top:12px;padding:10px 12px;background:#fff8e1;border-left:4px solid #f5a623;font-size:13px\"><b>Pagamento:</b> deve ser feito no valor certo, em dinheiro, num envelope fechado, e entregue juntamente com as aves.</div>"
            : "";

        var notasTransporte = totalTransporte > 0
            ? "<div style=\"margin-top:12px;padding:8px 12px;background:#fff8e1;border-left:4px solid #f5a623;font-size:12px\"><b>Notas sobre as aves de compra/venda:</b><ul style=\"margin:6px 0 0 18px;padding:0\"><li><b>Chegada prevista: 12h (hora belga).</b> O destinatário tem obrigatoriamente de estar presente a essa hora para receber as aves — <b>não há condições para as guardar por mais tempo</b>.</li><li>Sujeito a validação — limite total de <b>400 aves para transporte</b>, prioridade para aves de exposição. Confirmação por email <b>após o fecho das inscrições</b>.</li></ul></div>"
            : "";

        var anexoNota = forCriador
            ? "<p style=\"margin-top:16px;color:#666;font-size:12px\">Vai anexo o PDF com o detalhe completo das aves e o resumo de custos. Guarda-o para tua referência.</p>"
            : "<p style=\"margin-top:16px;color:#666;font-size:12px\">O detalhe das aves e o resumo de custos estão no PDF em anexo.</p>";

        return $"""
        <h3>{dadosHeader}</h3>
        <table cellpadding="4" style="border-collapse:collapse;font-size:13px">
          <tr><td><b>Nome:</b></td><td>{H(r.NomeCompleto)}</td></tr>
          <tr><td><b>País:</b></td><td>{H(r.Pais)}</td></tr>
          <tr><td><b>Email:</b></td><td>{H(r.Email)}</td></tr>
          <tr><td><b>Telefone:</b></td><td>{H(r.Telefone)}</td></tr>
          <tr><td><b>Nº STAM:</b></td><td>{H(r.NumeroStam)}</td></tr>
          <tr><td valign="top"><b>Local de recolha:</b></td><td>{H(localRecolha)}<br/><span style="color:#b45309;font-size:12px">Contacta o responsável do ponto de recolha para combinar a hora de entrega das aves.</span></td></tr>
        </table>

        <h3>Resumo</h3>
        <table cellpadding="4" style="border-collapse:collapse;font-size:13px">
          <tr><td><b>Aves para concurso:</b></td><td>{totalAves}</td></tr>
          {(totalVenda > 0 ? $"<tr><td><b>Aves para venda:</b></td><td>{totalVenda}</td></tr>" : "")}
          {(totalTransporte > 0 ? $"<tr><td><b>Aves para transporte (compra/venda):</b></td><td>{totalTransporte} <span style=\"color:#b45309\">(sujeitas a validação de espaço)</span></td></tr>" : "")}
        </table>

        {notasTransporte}
        {pagamento}
        {anexoNota}
        """;
    }

    private static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return clean.Length > 100 ? clean[..100] : clean;
    }
}
