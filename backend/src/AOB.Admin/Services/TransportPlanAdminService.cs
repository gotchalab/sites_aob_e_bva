using System.Text.Json;
using AOB.Application.Convoyage;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public class TransportPlanAdminService(AppDbContext db)
{
    public record YearConfig(int NumCargasAlvo, int CapacidadePorCarga, int MinPorCarga);

    public record SubmissionRow(
        int SubmissionId, string NomeCriador, string Email, string Telefone, string Pais,
        int? CollectionPointId, string ZonaNome, string? ZonaLocation,
        int NumAvesConcurso, int NumAvesVenda, int NumAvesTransporte,
        int NumAvesTransportePtBe, int NumAvesTransporteBePt,
        string SocioBva, decimal TotalPago,
        int? CargaId, string? CargaCodigo)
    {
        // Aves que ocupam o camião em cada sentido (o mesmo cálculo usado pelo planner).
        // Venda conta em ambos: as não vendidas regressam.
        public int AvesPtBe => NumAvesConcurso + NumAvesVenda + NumAvesTransportePtBe;
        public int AvesBePt => NumAvesConcurso + NumAvesVenda + NumAvesTransporteBePt;
    }

    public record CargaRow(
        int Id, string Codigo, string TransportadoraNome, string ZonasLabel,
        int SortOrder, string? Notas,
        List<CargaSubmissionRow> Submissoes)
    {
        public int TotalAves => Submissoes.Sum(s => s.NumAvesConcurso + s.NumAvesVenda + s.NumAvesTransporte);
    }

    public record CargaSubmissionRow(
        int Id, int SubmissionId, string NomeCriador,
        int NumAvesConcurso, int NumAvesVenda, int NumAvesTransporte,
        string ZonaNome, string? ZonaLocation);

    public record ZoneSuggestion(
        int CollectionPointId, string Nome, string? Location, int SortOrder,
        int NumInscricoes, int TotalAves, int CargasNecessarias);

    // Totais de ocupação do camião em cada sentido.
    //   AvesPtBe = concurso + venda + transporte "vende" (PT→BE)
    //   AvesBePt = concurso + venda + transporte "compra" (BE→PT)
    //              (venda conta em ambos: não vendidas regressam)
    //   AvesMax  = MAX(PtBe, BePt) por inscrição, somado — corresponde ao número
    //              mínimo real de gaiolas para servir ambos sentidos.
    public record DirectionTotals(int AvesPtBe, int AvesBePt, int AvesMax, int AvesSoma)
    {
        public int CargasPtBe(int cap) => Ceil(AvesPtBe, cap);
        public int CargasBePt(int cap) => Ceil(AvesBePt, cap);
        public int CargasMax(int cap)  => Ceil(AvesMax, cap);
        public int CargasSoma(int cap) => Ceil(AvesSoma, cap);
        private static int Ceil(int n, int cap) => n <= 0 || cap <= 0 ? 0 : (int)Math.Ceiling(n / (double)cap);
    }

    public record PlanOverview(
        YearConfig Config,
        List<ConvoyageCollectionPoint> Zones,
        List<ZoneSuggestion> Sugestoes,
        List<CargaRow> Cargas,
        List<SubmissionRow> SubmissoesSemCarga,
        List<SubmissionRow> SubmissoesSemPontoRecolha,
        int TotalInscricoes,
        int TotalAves,
        Dictionary<int, int> TotalAvesPorSubmissao,
        DirectionTotals Totais);

    // ── Config do ano ─────────────────────────────────────────────────────────

    public async Task<YearConfig> GetConfigAsync(int yearId)
    {
        var y = await db.ConvoyageYears.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == yearId)
            ?? throw new InvalidOperationException("Ano não encontrado.");
        return new YearConfig(y.NumCargasAlvo, y.CapacidadePorCarga, y.MinPorCarga);
    }

    public async Task<string?> UpdateConfigAsync(int yearId, YearConfig cfg)
    {
        if (cfg.NumCargasAlvo < 1) return "Número de cargas deve ser >= 1.";
        if (cfg.CapacidadePorCarga < 1) return "Capacidade por carga deve ser >= 1.";
        if (cfg.MinPorCarga < 0 || cfg.MinPorCarga > cfg.CapacidadePorCarga)
            return "Mínimo por carga tem de estar entre 0 e a capacidade.";

        var y = await db.ConvoyageYears.FirstOrDefaultAsync(x => x.Id == yearId);
        if (y is null) return "Ano não encontrado.";

        y.NumCargasAlvo = cfg.NumCargasAlvo;
        y.CapacidadePorCarga = cfg.CapacidadePorCarga;
        y.MinPorCarga = cfg.MinPorCarga;

        await db.SaveChangesAsync();
        return null;
    }

    // ── Snapshot ──────────────────────────────────────────────────────────────

    public async Task<PlanOverview?> GetOverviewAsync(int yearId)
    {
        var y = await db.ConvoyageYears.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == yearId);
        if (y is null) return null;

        var config = new YearConfig(y.NumCargasAlvo, y.CapacidadePorCarga, y.MinPorCarga);

        var zones = await db.ConvoyageCollectionPoints.AsNoTracking()
            .Where(p => p.ConvoyageYearId == yearId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        var subs = await db.FormSubmissions.AsNoTracking()
            .Where(f => f.ConvoyageYearId == yearId
                     && f.FormType == FormType.InscricaoConvoyage)
            .OrderBy(f => f.SubmittedAt)
            .ToListAsync();

        var cargaAssignments = await db.TransportCargaSubmissions.AsNoTracking()
            .Where(cs => cs.TransportCarga.ConvoyageYearId == yearId)
            .Select(cs => new
            {
                cs.Id,
                cs.TransportCargaId,
                cs.FormSubmissionId,
                CargaCodigo = cs.TransportCarga.Codigo,
                cs.NumAvesConcurso,
                cs.NumAvesVenda,
                cs.NumAvesTransporte,
            })
            .ToListAsync();

        var cargas = await db.TransportCargas.AsNoTracking()
            .Where(c => c.ConvoyageYearId == yearId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var zonesById = zones.ToDictionary(z => z.Id, z => z);
        // Uma submissão pode agora estar em várias cargas (divisão concurso/venda),
        // por isso agrupamos em lookup em vez de dicionário 1-para-1.
        var assignmentsBySubmission = cargaAssignments
            .GroupBy(a => a.FormSubmissionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var submissionRows = subs.Select(s =>
        {
            var m = ParseSubmission(s.DataJson);
            var pointId = s.LocalRecolhaId;
            ConvoyageCollectionPoint? pt = null;
            if (pointId.HasValue) zonesById.TryGetValue(pointId.Value, out pt);
            var zonaNome = pt?.Name
                ?? (string.IsNullOrWhiteSpace(m.LocalRecolha) ? "—" : m.LocalRecolha);
            var zonaLocation = pt?.Location;

            // Se a submissão estiver dividida, mostramos a primeira carga como
            // referência principal — o UI usa `cargaRows` para ver todas as partes.
            var subAssignments = assignmentsBySubmission.TryGetValue(s.Id, out var lst)
                ? lst : new();
            var firstAssignment = subAssignments.FirstOrDefault();
            return new SubmissionRow(
                s.Id, m.Nome, m.Email, m.Telefone, m.Pais,
                pointId, zonaNome, zonaLocation,
                m.NumAvesConcurso, m.NumAvesVenda, m.NumAvesTransporte,
                m.NumAvesTransportePtBe, m.NumAvesTransporteBePt,
                m.SocioBvaLabel, m.TotalPago,
                firstAssignment?.TransportCargaId, firstAssignment?.CargaCodigo);
        }).ToList();

        var cargaSubmissionsByCarga = cargaAssignments
            .GroupBy(a => a.TransportCargaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var submissionMeta = submissionRows.ToDictionary(r => r.SubmissionId, r => r);

        var cargaRows = cargas.Select(c =>
        {
            var items = cargaSubmissionsByCarga.TryGetValue(c.Id, out var lst)
                ? lst.Select(a =>
                {
                    submissionMeta.TryGetValue(a.FormSubmissionId, out var s);
                    return new CargaSubmissionRow(
                        a.Id, a.FormSubmissionId,
                        s?.NomeCriador ?? "(desconhecido)",
                        a.NumAvesConcurso, a.NumAvesVenda, a.NumAvesTransporte,
                        s?.ZonaNome ?? "—", s?.ZonaLocation);
                }).ToList()
                : new List<CargaSubmissionRow>();

            return new CargaRow(c.Id, c.Codigo, c.TransportadoraNome, c.ZonasLabel,
                c.SortOrder, c.Notas, items);
        }).ToList();

        var semCarga = submissionRows.Where(r => r.CargaId is null && r.CollectionPointId is not null).ToList();
        var semPonto = submissionRows.Where(r => r.CollectionPointId is null).ToList();

        // Sugestões por ponto de recolha (ceil(aves / capacidade))
        var perPoint = submissionRows
            .Where(r => r.CollectionPointId is not null)
            .GroupBy(r => r.CollectionPointId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new PointStats(
                    Inscricoes: g.Count(),
                    Aves: g.Sum(x => x.NumAvesConcurso + x.NumAvesVenda + x.NumAvesTransporte)));

        var sugestoes = zones.Select(z =>
        {
            var stats = perPoint.TryGetValue(z.Id, out var v) ? v : new PointStats(0, 0);
            var cargas = stats.Aves == 0
                ? 0
                : (int)Math.Ceiling(stats.Aves / (double)y.CapacidadePorCarga);
            return new ZoneSuggestion(
                z.Id, z.Name, z.Location, z.SortOrder,
                stats.Inscricoes, stats.Aves, cargas);
        }).ToList();

        var totalAvesPorSubmissao = submissionRows.ToDictionary(
            r => r.SubmissionId,
            r => r.NumAvesConcurso + r.NumAvesVenda + r.NumAvesTransporte);

        var elegiveis = submissionRows.Where(r => r.CollectionPointId is not null).ToList();
        var totais = new DirectionTotals(
            AvesPtBe: elegiveis.Sum(r => r.AvesPtBe),
            AvesBePt: elegiveis.Sum(r => r.AvesBePt),
            AvesMax:  elegiveis.Sum(r => Math.Max(r.AvesPtBe, r.AvesBePt)),
            AvesSoma: elegiveis.Sum(r => r.NumAvesConcurso + r.NumAvesVenda + r.NumAvesTransporte));

        return new PlanOverview(
            config, zones, sugestoes, cargaRows, semCarga, semPonto,
            submissionRows.Count,
            submissionRows.Sum(r => r.NumAvesConcurso + r.NumAvesVenda + r.NumAvesTransporte),
            totalAvesPorSubmissao,
            totais);
    }

    // ── Gerar / limpar plano ─────────────────────────────────────────────────

    public async Task<string?> GeneratePlanAsync(
        int yearId,
        TransportPlanner.PlanMode mode = TransportPlanner.PlanMode.Total)
    {
        var y = await db.ConvoyageYears
            .Include(x => x.CollectionPoints)
            .FirstOrDefaultAsync(x => x.Id == yearId);
        if (y is null) return "Ano não encontrado.";

        var zonesSorted = y.CollectionPoints.OrderBy(p => p.SortOrder).ToList();
        if (zonesSorted.Count == 0)
            return "Não existem pontos de recolha para este ano.";

        var subs = await db.FormSubmissions.AsNoTracking()
            .Where(f => f.ConvoyageYearId == yearId
                     && f.FormType == FormType.InscricaoConvoyage
                     && f.LocalRecolhaId != null)
            .ToListAsync();

        var plannerSubs = subs.Select(s =>
        {
            var m = ParseSubmission(s.DataJson);
            var point = zonesSorted.FirstOrDefault(z => z.Id == s.LocalRecolhaId!.Value);
            var pointName = point?.Name ?? "(desconhecido)";
            return new TransportPlanner.SubmissionInput(
                s.Id, m.Nome, s.LocalRecolhaId!.Value, pointName,
                m.NumAvesConcurso, m.NumAvesVenda,
                m.NumAvesTransportePtBe, m.NumAvesTransporteBePt);
        }).ToList();

        var zoneInputs = zonesSorted
            .Select(z => new TransportPlanner.ZoneInput(z.Id, z.Name, z.Location, z.SortOrder))
            .ToList();

        var config = new TransportPlanner.PlannerConfig(
            y.CapacidadePorCarga, y.MinPorCarga, y.NumCargasAlvo, mode);

        var plan = TransportPlanner.Plan(plannerSubs, zoneInputs, config);

        // Substitui plano existente.
        var existing = await db.TransportCargas
            .Where(c => c.ConvoyageYearId == yearId)
            .ToListAsync();
        db.TransportCargas.RemoveRange(existing);
        await db.SaveChangesAsync();

        foreach (var p in plan)
        {
            var carga = new TransportCarga
            {
                ConvoyageYearId = yearId,
                Codigo = p.Codigo,
                TransportadoraNome = p.TransportadoraNome,
                ZonasLabel = p.ZonasLabel,
                SortOrder = p.SortOrder,
            };
            db.TransportCargas.Add(carga);
            await db.SaveChangesAsync();

            foreach (var s in p.Submissoes)
            {
                db.TransportCargaSubmissions.Add(new TransportCargaSubmission
                {
                    TransportCargaId = carga.Id,
                    FormSubmissionId = s.SubmissionId,
                    NumAvesConcurso = s.NumAvesConcurso,
                    NumAvesVenda = s.NumAvesVenda,
                    NumAvesTransporte = s.NumAvesTransporte,
                });
            }
        }
        await db.SaveChangesAsync();
        return null;
    }

    public async Task ClearPlanAsync(int yearId)
    {
        var existing = await db.TransportCargas
            .Where(c => c.ConvoyageYearId == yearId)
            .ToListAsync();
        db.TransportCargas.RemoveRange(existing);
        await db.SaveChangesAsync();
    }

    // ── Ajustes manuais ──────────────────────────────────────────────────────

    // Move parte (ou toda) uma linha para outra carga. Se `quantidade` for menor
    // que o total da linha, divide: retira essas aves da origem e cria/consolida
    // na carga alvo. Prioriza tirar do tipo com mais aves. Se `quantidade` for
    // igual ao total, comporta-se como move total. Se targetCargaId é null,
    // remove essa quantidade da carga origem (sem destino).
    public async Task<string?> SplitAndMoveAsync(int yearId, int assignmentId, int quantidade, int? targetCargaId)
    {
        var existing = await db.TransportCargaSubmissions
            .Include(cs => cs.TransportCarga)
            .FirstOrDefaultAsync(cs => cs.Id == assignmentId
                                    && cs.TransportCarga.ConvoyageYearId == yearId);
        if (existing is null) return "Linha não encontrada.";

        var total = existing.NumAvesConcurso + existing.NumAvesVenda + existing.NumAvesTransporte;
        if (quantidade <= 0) return "Quantidade tem de ser positiva.";
        if (quantidade > total) return $"Quantidade máxima é {total}.";

        if (quantidade == total)
            return await MoveAssignmentAsync(yearId, assignmentId, targetCargaId);

        // Divisão parcial. Retira das aves pela ordem: transporte, venda, concurso
        // (preserva sempre o máximo de aves de concurso na carga original).
        int remaining = quantidade;
        int moveTransporte = Math.Min(existing.NumAvesTransporte, remaining);
        remaining -= moveTransporte;
        int moveVenda = Math.Min(existing.NumAvesVenda, remaining);
        remaining -= moveVenda;
        int moveConcurso = Math.Min(existing.NumAvesConcurso, remaining);

        // Actualiza a origem com o resto.
        existing.NumAvesConcurso -= moveConcurso;
        existing.NumAvesVenda -= moveVenda;
        existing.NumAvesTransporte -= moveTransporte;

        if (targetCargaId is null)
        {
            await db.SaveChangesAsync();
            return null;
        }

        var targetCarga = await db.TransportCargas
            .FirstOrDefaultAsync(c => c.Id == targetCargaId && c.ConvoyageYearId == yearId);
        if (targetCarga is null) return "Carga alvo não encontrada.";

        var existingOnTarget = await db.TransportCargaSubmissions
            .FirstOrDefaultAsync(cs => cs.TransportCargaId == targetCargaId
                                    && cs.FormSubmissionId == existing.FormSubmissionId
                                    && cs.Id != existing.Id);
        if (existingOnTarget is not null)
        {
            existingOnTarget.NumAvesConcurso += moveConcurso;
            existingOnTarget.NumAvesVenda += moveVenda;
            existingOnTarget.NumAvesTransporte += moveTransporte;
        }
        else
        {
            db.TransportCargaSubmissions.Add(new TransportCargaSubmission
            {
                TransportCargaId = targetCargaId.Value,
                FormSubmissionId = existing.FormSubmissionId,
                NumAvesConcurso = moveConcurso,
                NumAvesVenda = moveVenda,
                NumAvesTransporte = moveTransporte,
            });
        }
        await db.SaveChangesAsync();
        return null;
    }

    // Move uma linha específica (identificada pelo Id do TransportCargaSubmission)
    // para outra carga ou remove-a. Se a carga alvo já tem outra linha da mesma
    // submissão, consolida (soma valores) em vez de criar duplicado.
    public async Task<string?> MoveAssignmentAsync(int yearId, int assignmentId, int? targetCargaId)
    {
        var existing = await db.TransportCargaSubmissions
            .Include(cs => cs.TransportCarga)
            .FirstOrDefaultAsync(cs => cs.Id == assignmentId
                                    && cs.TransportCarga.ConvoyageYearId == yearId);
        if (existing is null) return "Linha não encontrada.";

        if (targetCargaId is null)
        {
            db.TransportCargaSubmissions.Remove(existing);
            await db.SaveChangesAsync();
            return null;
        }

        var carga = await db.TransportCargas
            .FirstOrDefaultAsync(c => c.Id == targetCargaId && c.ConvoyageYearId == yearId);
        if (carga is null) return "Carga alvo não encontrada.";

        var existingOnTarget = await db.TransportCargaSubmissions
            .FirstOrDefaultAsync(cs => cs.TransportCargaId == targetCargaId
                                    && cs.FormSubmissionId == existing.FormSubmissionId
                                    && cs.Id != existing.Id);
        if (existingOnTarget is not null)
        {
            existingOnTarget.NumAvesConcurso += existing.NumAvesConcurso;
            existingOnTarget.NumAvesVenda += existing.NumAvesVenda;
            existingOnTarget.NumAvesTransporte += existing.NumAvesTransporte;
            db.TransportCargaSubmissions.Remove(existing);
        }
        else
        {
            existing.TransportCargaId = targetCargaId.Value;
        }
        await db.SaveChangesAsync();
        return null;
    }

    // Atribui uma submissão (ainda sem carga) a uma carga. Usa os totais da
    // inscrição inteira. Se a carga alvo já tem uma linha desta submissão,
    // consolida em vez de duplicar.
    public async Task<string?> AssignSubmissionToCargaAsync(int yearId, int submissionId, int? targetCargaId)
    {
        if (targetCargaId is null) return null;

        var carga = await db.TransportCargas
            .FirstOrDefaultAsync(c => c.Id == targetCargaId && c.ConvoyageYearId == yearId);
        if (carga is null) return "Carga alvo não encontrada.";

        var sub = await db.FormSubmissions
            .FirstOrDefaultAsync(f => f.Id == submissionId && f.ConvoyageYearId == yearId);
        if (sub is null) return "Inscrição não encontrada.";

        var m = ParseSubmission(sub.DataJson);

        var existingOnTarget = await db.TransportCargaSubmissions
            .FirstOrDefaultAsync(cs => cs.TransportCargaId == targetCargaId
                                    && cs.FormSubmissionId == submissionId);
        if (existingOnTarget is not null)
        {
            existingOnTarget.NumAvesConcurso = m.NumAvesConcurso;
            existingOnTarget.NumAvesVenda = m.NumAvesVenda;
            existingOnTarget.NumAvesTransporte = m.NumAvesTransporte;
        }
        else
        {
            db.TransportCargaSubmissions.Add(new TransportCargaSubmission
            {
                TransportCargaId = targetCargaId.Value,
                FormSubmissionId = submissionId,
                NumAvesConcurso = m.NumAvesConcurso,
                NumAvesVenda = m.NumAvesVenda,
                NumAvesTransporte = m.NumAvesTransporte,
            });
        }
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> UpdateCargaAsync(
        int yearId, int cargaId, string transportadoraNome, string zonasLabel, string? notas)
    {
        var c = await db.TransportCargas
            .FirstOrDefaultAsync(x => x.Id == cargaId && x.ConvoyageYearId == yearId);
        if (c is null) return "Carga não encontrada.";
        if (string.IsNullOrWhiteSpace(zonasLabel)) return "Zonas obrigatórias.";

        c.TransportadoraNome = (transportadoraNome ?? "").Trim();
        c.ZonasLabel = zonasLabel.Trim();
        c.Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<TransportCarga?> AddCargaAsync(int yearId, string zonasLabel, string transportadoraNome)
    {
        var y = await db.ConvoyageYears.FirstOrDefaultAsync(x => x.Id == yearId);
        if (y is null) return null;

        var maxOrder = await db.TransportCargas
            .Where(c => c.ConvoyageYearId == yearId)
            .MaxAsync(c => (int?)c.SortOrder) ?? 0;
        var order = maxOrder + 1;

        var carga = new TransportCarga
        {
            ConvoyageYearId = yearId,
            Codigo = $"T{order:00}",
            SortOrder = order,
            ZonasLabel = string.IsNullOrWhiteSpace(zonasLabel) ? "—" : zonasLabel.Trim(),
            TransportadoraNome = (transportadoraNome ?? "").Trim(),
        };
        db.TransportCargas.Add(carga);
        await db.SaveChangesAsync();
        return carga;
    }

    public async Task<string?> DeleteCargaAsync(int yearId, int cargaId)
    {
        var c = await db.TransportCargas
            .FirstOrDefaultAsync(x => x.Id == cargaId && x.ConvoyageYearId == yearId);
        if (c is null) return "Carga não encontrada.";
        db.TransportCargas.Remove(c);
        await db.SaveChangesAsync();
        return null;
    }

    // Associa uma inscrição sem LocalRecolhaId a um ponto de recolha.
    public async Task<string?> AssignPointAsync(int yearId, int submissionId, int pointId)
    {
        var sub = await db.FormSubmissions
            .FirstOrDefaultAsync(f => f.Id == submissionId && f.ConvoyageYearId == yearId);
        if (sub is null) return "Inscrição não encontrada.";
        var point = await db.ConvoyageCollectionPoints
            .FirstOrDefaultAsync(p => p.Id == pointId && p.ConvoyageYearId == yearId);
        if (point is null) return "Ponto de recolha não encontrado.";
        sub.LocalRecolhaId = pointId;
        await db.SaveChangesAsync();
        return null;
    }

    // Tenta ligar automaticamente inscrições sem LocalRecolhaId aos pontos
    // existentes com base no campo textual `LocalRecolha` do JSON.
    // Devolve (matched, unmatched).
    public async Task<(int Matched, int Unmatched)> BackfillLocalRecolhaAsync(int yearId)
    {
        var zones = await db.ConvoyageCollectionPoints
            .Where(p => p.ConvoyageYearId == yearId)
            .ToListAsync();
        if (zones.Count == 0) return (0, 0);

        var subs = await db.FormSubmissions
            .Where(f => f.ConvoyageYearId == yearId
                     && f.FormType == FormType.InscricaoConvoyage
                     && f.LocalRecolhaId == null)
            .ToListAsync();

        int matched = 0, unmatched = 0;
        foreach (var s in subs)
        {
            var m = ParseSubmission(s.DataJson);
            var local = m.LocalRecolha?.Trim() ?? "";
            var hit = zones.FirstOrDefault(z =>
                        string.Equals(z.Name, local, StringComparison.OrdinalIgnoreCase))
                  ?? zones.FirstOrDefault(z =>
                        string.Equals($"{z.Name} ({z.Location})", local, StringComparison.OrdinalIgnoreCase))
                  ?? zones.FirstOrDefault(z =>
                        local.Contains(z.Name, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                s.LocalRecolhaId = hit.Id;
                matched++;
            }
            else
            {
                unmatched++;
            }
        }
        if (matched > 0) await db.SaveChangesAsync();
        return (matched, unmatched);
    }

    // ── Exportação Excel ─────────────────────────────────────────────────────

    public async Task<byte[]?> ExportXlsxAsync(int yearId)
    {
        var overview = await GetOverviewAsync(yearId);
        if (overview is null) return null;

        var y = await db.ConvoyageYears.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == yearId);
        if (y is null) return null;

        // Uma submissão pode estar em várias cargas (divisão concurso/venda),
        // por isso agrupamos as cargas por submissionId em vez de dictionar 1-para-1.
        var cargasBySubmissionId = overview.Cargas
            .SelectMany(c => c.Submissoes.Select(s => (SubId: s.SubmissionId, Carga: c)))
            .GroupBy(x => x.SubId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Carga).OrderBy(c => c.SortOrder).ToList());

        string CargaLabelFor(int subId) =>
            cargasBySubmissionId.TryGetValue(subId, out var cs)
                ? string.Join(" + ", cs.Select(c => c.Codigo))
                : "—";

        // Filas por (submissionId, tipo) com uma entrada de código T por cada ave —
        // consumidas por ordem quando montamos as linhas da folha "Aves". Isto dá
        // uma atribuição determinística das anilhas às cargas específicas, mesmo
        // quando a inscrição está partida (ex.: 18c em T06, 5v em T07).
        var cargasPorAve = new Dictionary<(int SubId, string Tipo), Queue<string>>();
        foreach (var (subId, cargas) in cargasBySubmissionId)
        {
            foreach (var tipo in new[] { "concurso", "venda", "transporte" })
            {
                var q = new Queue<string>();
                foreach (var c in cargas)
                {
                    var s = c.Submissoes.FirstOrDefault(x => x.SubmissionId == subId);
                    if (s is null) continue;
                    var n = tipo switch
                    {
                        "concurso"   => s.NumAvesConcurso,
                        "venda"      => s.NumAvesVenda,
                        "transporte" => s.NumAvesTransporte,
                        _            => 0,
                    };
                    for (int i = 0; i < n; i++) q.Enqueue(c.Codigo);
                }
                cargasPorAve[(subId, tipo)] = q;
            }
        }

        string CargaParaAve(int subId, string tipo)
        {
            if (cargasPorAve.TryGetValue((subId, tipo), out var q) && q.Count > 0)
                return q.Dequeue();
            return CargaLabelFor(subId);
        }

        // Descreve as aves de uma inscrição atribuídas a UMA carga, agrupadas por
        // tipo (18 concurso, 5 venda, ...). Serve para clarificar splits na folha
        // "Transportes" — quando uma inscrição está partida por várias cargas,
        // cada linha mostra apenas as aves dessa carga.
        static string DescribeParte(CargaSubmissionRow s)
        {
            var parts = new List<string>();
            if (s.NumAvesConcurso   > 0) parts.Add($"{s.NumAvesConcurso} concurso");
            if (s.NumAvesVenda      > 0) parts.Add($"{s.NumAvesVenda} venda");
            if (s.NumAvesTransporte > 0) parts.Add($"{s.NumAvesTransporte} transporte");
            var detalhe = parts.Count == 0 ? "0" : string.Join(" + ", parts);
            return $"{s.NomeCriador.ToUpperInvariant()} ({detalhe})";
        }

        var transportes = overview.Cargas
            .OrderBy(c => c.SortOrder)
            .Select(c => new TransportExcelExporter.TransporteRow(
                Transportadora: c.TransportadoraNome,
                Codigo: c.Codigo,
                NumAves: c.TotalAves,
                Zonas: c.ZonasLabel,
                CriadoresLabel: string.Join(", ", c.Submissoes.Select(DescribeParte)),
                Tipo: "Agapornis",
                Sobras: Math.Max(0, overview.Config.CapacidadePorCarga - c.TotalAves)))
            .ToList();

        var submissoes = await db.FormSubmissions.AsNoTracking()
            .Where(f => f.ConvoyageYearId == yearId
                     && f.FormType == FormType.InscricaoConvoyage)
            .OrderBy(f => f.SubmittedAt)
            .ToListAsync();

        // Fonte de verdade para o local de recolha: a FK LocalRecolhaId; o campo textual
        // no JSON pode faltar em inscrições antigas.
        var pointsById = overview.Zones.ToDictionary(z => z.Id, z => z);
        string LocalRecolhaLabelFor(FormSubmission s, string jsonFallback)
        {
            if (s.LocalRecolhaId is int pid && pointsById.TryGetValue(pid, out var pt))
                return string.IsNullOrWhiteSpace(pt.Location) ? pt.Name : $"{pt.Name} ({pt.Location})";
            return string.IsNullOrWhiteSpace(jsonFallback) ? "" : jsonFallback;
        }

        var inscricoes = submissoes.Select(s =>
        {
            var m = ParseSubmission(s.DataJson);
            return new TransportExcelExporter.InscricaoRow(
                SubmissionId: s.Id,
                SubmittedAt: s.SubmittedAt,
                Nome: m.Nome,
                Email: m.Email,
                Telefone: m.Telefone,
                Pais: m.Pais,
                LocalRecolha: LocalRecolhaLabelFor(s, m.LocalRecolha),
                NumAvesConcurso: m.NumAvesConcurso,
                NumAvesVenda: m.NumAvesVenda,
                NumAvesTransportePtBe: m.NumAvesTransportePtBe,
                NumAvesTransporteBePt: m.NumAvesTransporteBePt,
                SocioBva: m.SocioBvaLabel,
                TotalPago: m.TotalPago,
                CargaAtribuida: CargaLabelFor(s.Id));
        }).ToList();

        var aves = new List<TransportExcelExporter.AveRow>();
        foreach (var s in submissoes)
        {
            var m = ParseSubmission(s.DataJson);

            void AddAves(IEnumerable<AveMeta> lista, string tipo, string tipoLabel)
            {
                foreach (var a in lista)
                {
                    aves.Add(new TransportExcelExporter.AveRow(
                        SubmissionId: s.Id,
                        Criador: m.Nome,
                        Serie: a.Serie,
                        Especie: a.Especie,
                        Mutacao: a.Mutacao,
                        Anilha: a.Anilha,
                        Equipa: string.IsNullOrEmpty(a.EquipaId) ? "—" : "T",
                        Posicao: a.Posicao ?? "—",
                        Tipo: tipoLabel,
                        CargaAtribuida: CargaParaAve(s.Id, tipo)));
                }
            }

            AddAves(m.Aves,           "concurso",   "Concurso");
            AddAves(m.AvesVenda,      "venda",      "Venda");
            AddAves(m.AvesTransporte, "transporte", "Transporte");
        }

        var pricing = new TransportExcelExporter.Pricing(
            y.PrecoInscricao, y.PrecoAveBva, y.PrecoGaiola,
            y.TarifaTransporteSocio, y.TarifaTransporteNaoSocio,
            y.TarifaAdquirenteSocio, y.TarifaAdquirenteNaoSocio,
            y.Quota);

        return TransportExcelExporter.Render(
            y.Year, "Agapornis", overview.Config.CapacidadePorCarga, pricing,
            transportes, inscricoes, aves);
    }

    // ── Exportação de etiquetas Avery 3421 ───────────────────────────────────
    // Devolve o PDF pronto + um slug curto do âmbito para compor o nome do
    // ficheiro no endpoint (ex.: "pacos-de-ferreira", "filipe-lopes-99").

    public record EtiquetasResult(byte[] Bytes, string ScopeSlug);

    public async Task<EtiquetasResult?> ExportEtiquetasPorPontoAsync(int yearId, int pointId)
    {
        var point = await db.ConvoyageCollectionPoints.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pointId && p.ConvoyageYearId == yearId);
        if (point is null) return null;

        var subs = await db.FormSubmissions.AsNoTracking()
            .Where(f => f.ConvoyageYearId == yearId
                     && f.FormType == FormType.InscricaoConvoyage
                     && f.LocalRecolhaId == pointId)
            .OrderBy(f => f.SubmittedAt)
            .ToListAsync();

        var labels = new List<EtiquetasAvery3421PdfGenerator.EtiquetaLabel>();
        foreach (var s in subs)
            labels.AddRange(BuildLabelsFromJson(s.DataJson));

        var bytes = EtiquetasAvery3421PdfGenerator.Render(labels);
        return new EtiquetasResult(bytes, Slugify(point.Name));
    }

    public async Task<EtiquetasResult?> ExportEtiquetasPorInscricaoAsync(int yearId, int submissionId)
    {
        var sub = await db.FormSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == submissionId
                                   && f.ConvoyageYearId == yearId
                                   && f.FormType == FormType.InscricaoConvoyage);
        if (sub is null) return null;

        var labels = BuildLabelsFromJson(sub.DataJson);
        var bytes = EtiquetasAvery3421PdfGenerator.Render(labels);

        var meta = ParseSubmission(sub.DataJson);
        var slug = Slugify(meta.Nome);
        if (string.IsNullOrEmpty(slug)) slug = $"insc-{submissionId}";
        else slug = $"{slug}-{submissionId}";
        return new EtiquetasResult(bytes, slug);
    }

    // Constrói a lista de etiquetas de UMA inscrição pela ordem visual esperada:
    // Concurso → Venda → Transporte. Só se emite etiqueta para aves com anilha.
    private static List<EtiquetasAvery3421PdfGenerator.EtiquetaLabel> BuildLabelsFromJson(string json)
    {
        var result = new List<EtiquetasAvery3421PdfGenerator.EtiquetaLabel>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            string nome = GetStr(r, "NomeCompleto");
            if (string.IsNullOrWhiteSpace(nome)) return result;

            // Concurso — Serie + EspecieMutacao + Anilha
            foreach (var a in EnumerateArray(r, "Aves"))
            {
                var anilha = GetStr(a, "Anilha");
                if (string.IsNullOrWhiteSpace(anilha)) continue;
                var mutacao = GetStr(a, "EspecieMutacao");
                var serie   = GetStr(a, "Serie");
                result.Add(new EtiquetasAvery3421PdfGenerator.EtiquetaLabel(
                    Nome: nome,
                    Anilha: anilha,
                    LinhaDescricao: mutacao,
                    LinhaTipoOuSerie: serie,
                    Tipo: EtiquetasAvery3421PdfGenerator.EtiquetaTipo.Concurso));
            }

            // Venda — EspecieMutacao + Anilha, rótulo "VENDAS"
            foreach (var a in EnumerateArray(r, "AvesVenda"))
            {
                var anilha = GetStr(a, "Anilha");
                if (string.IsNullOrWhiteSpace(anilha)) continue;
                var mutacao = GetStr(a, "EspecieMutacao");
                result.Add(new EtiquetasAvery3421PdfGenerator.EtiquetaLabel(
                    Nome: nome,
                    Anilha: anilha,
                    LinhaDescricao: mutacao,
                    LinhaTipoOuSerie: "VENDAS",
                    Tipo: EtiquetasAvery3421PdfGenerator.EtiquetaTipo.Venda));
            }

            // Transporte — "Entregar a: X" + Anilha, rótulo "TRANSPORTE"
            foreach (var a in EnumerateArray(r, "AvesTransporte"))
            {
                var anilha = GetStr(a, "Anilha");
                if (string.IsNullOrWhiteSpace(anilha)) continue;
                var destinatario = GetStr(a, "DestinatarioNome");
                var linhaDesc = string.IsNullOrWhiteSpace(destinatario)
                    ? ""
                    : $"Entregar a: {destinatario}";
                result.Add(new EtiquetasAvery3421PdfGenerator.EtiquetaLabel(
                    Nome: nome,
                    Anilha: anilha,
                    LinhaDescricao: linhaDesc,
                    LinhaTipoOuSerie: "TRANSPORTE",
                    Tipo: EtiquetasAvery3421PdfGenerator.EtiquetaTipo.Transporte));
            }
        }
        catch
        {
            // JSON corrompido: retorna lista parcial ou vazia.
        }
        return result;
    }

    // Helpers de leitura tolerantes ao casing (Pascal vs camel) usados no JSON
    // — mesma abordagem que ParseSubmission.
    private static string GetStr(JsonElement el, string key)
    {
        foreach (var candidate in new[] { key, Camel(key) })
            if (el.TryGetProperty(candidate, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
        return "";
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement el, string key)
    {
        foreach (var candidate in new[] { key, Camel(key) })
            if (el.TryGetProperty(candidate, out var v) && v.ValueKind == JsonValueKind.Array)
                return v.EnumerateArray();
        return Array.Empty<JsonElement>();
    }

    // Slug estilo kebab-case ascii — usado apenas para compor nomes de ficheiro.
    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var norm = value.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(norm.Length);
        foreach (var c in norm)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        var s = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        s = System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s;
    }

    // ── Parse do DataJson ────────────────────────────────────────────────────

    private record AveMeta(string Serie, string Especie, string Mutacao, string Anilha, string? EquipaId, string? Posicao);
    private record SubmissionMeta(
        string Nome, string Email, string Telefone, string Pais, string LocalRecolha,
        int NumAvesConcurso, int NumAvesVenda, int NumAvesTransporte,
        int NumAvesTransportePtBe, int NumAvesTransporteBePt,
        string SocioBvaLabel, string SocioBvaStatus, decimal TotalPago,
        List<AveMeta> Aves,
        List<AveMeta> AvesVenda,
        List<AveMeta> AvesTransporte);

    private static SubmissionMeta ParseSubmission(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            string S(string k, JsonElement el)
            {
                foreach (var candidate in new[] { k, Camel(k) })
                {
                    if (el.TryGetProperty(candidate, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString() ?? "";
                }
                return "";
            }
            int I(string k, JsonElement el)
            {
                foreach (var candidate in new[] { k, Camel(k) })
                {
                    if (el.TryGetProperty(candidate, out var v) && v.ValueKind == JsonValueKind.Number)
                        return v.GetInt32();
                }
                return 0;
            }
            decimal D(JsonElement el, string k)
            {
                foreach (var candidate in new[] { k, Camel(k) })
                {
                    if (el.TryGetProperty(candidate, out var v) && v.ValueKind == JsonValueKind.Number)
                        return v.GetDecimal();
                }
                return 0m;
            }

            var aves = new List<AveMeta>();
            JsonElement avesEl = default;
            if (r.TryGetProperty("Aves", out avesEl) || r.TryGetProperty("aves", out avesEl))
            {
                if (avesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in avesEl.EnumerateArray())
                    {
                        aves.Add(new AveMeta(
                            S("Serie", a), S("Especie", a), S("EspecieMutacao", a),
                            S("Anilha", a),
                            a.TryGetProperty("EquipaId", out var eq) && eq.ValueKind == JsonValueKind.String
                                ? eq.GetString() : null,
                            a.TryGetProperty("PosicaoEquipa", out var pos) && pos.ValueKind == JsonValueKind.String
                                ? pos.GetString() : null));
                    }
                }
            }

            var numConcurso = aves.Count;

            var avesVenda = new List<AveMeta>();
            JsonElement vendaEl = default;
            if ((r.TryGetProperty("AvesVenda", out vendaEl) && vendaEl.ValueKind == JsonValueKind.Array)
                || (r.TryGetProperty("avesVenda", out vendaEl) && vendaEl.ValueKind == JsonValueKind.Array))
            {
                foreach (var a in vendaEl.EnumerateArray())
                {
                    avesVenda.Add(new AveMeta(
                        S("Serie", a), S("Especie", a), S("EspecieMutacao", a),
                        S("Anilha", a), null, null));
                }
            }
            int numVenda = avesVenda.Count;

            var avesTransporte = new List<AveMeta>();
            int numTransporteVende = 0;   // PT→BE
            int numTransporteCompra = 0;  // BE→PT
            JsonElement transporteArr = default;
            var hasTransporte = (r.TryGetProperty("AvesTransporte", out transporteArr) && transporteArr.ValueKind == JsonValueKind.Array)
                             || (r.TryGetProperty("avesTransporte", out transporteArr) && transporteArr.ValueKind == JsonValueKind.Array);
            if (hasTransporte)
            {
                foreach (var a in transporteArr.EnumerateArray())
                {
                    avesTransporte.Add(new AveMeta(
                        S("Serie", a), S("Especie", a), S("EspecieMutacao", a),
                        S("Anilha", a), null, null));
                    var origem = S("Origem", a);
                    if (string.Equals(origem, "Vende", StringComparison.OrdinalIgnoreCase))
                        numTransporteVende++;
                    else if (string.Equals(origem, "Compra", StringComparison.OrdinalIgnoreCase))
                        numTransporteCompra++;
                }
            }
            int numTransporte = avesTransporte.Count;

            var totalAves = I("TotalAves", r);
            if (totalAves > numConcurso) numConcurso = totalAves;

            var status = S("SocioBvaStatus", r);
            // Labels curtos/intuitivos — as fórmulas do Excel comparam com estes valores.
            var socioLabel = status switch
            {
                "JaSocio"          => "Sócio",
                "PagaComInscricao" => "Paga na inscrição",
                "NaoSocio"         => "Não sócio",
                _ => S("SocioBva", r) == "True" ? "Sócio" : "Não sócio",
            };

            decimal totalPago = 0m;
            if (r.TryGetProperty("Custos", out var custos) && custos.ValueKind == JsonValueKind.Object)
                totalPago = D(custos, "total");

            var socioStatus = status switch
            {
                "JaSocio" or "PagaComInscricao" or "NaoSocio" => status,
                _ => S("SocioBva", r) == "True" ? "JaSocio" : "NaoSocio",
            };

            return new SubmissionMeta(
                S("NomeCompleto", r), S("Email", r), S("Telefone", r),
                S("Pais", r), S("LocalRecolha", r),
                numConcurso, numVenda, numTransporte,
                numTransporteVende, numTransporteCompra,
                socioLabel, socioStatus, totalPago,
                aves, avesVenda, avesTransporte);
        }
        catch
        {
            return new SubmissionMeta("", "", "", "", "", 0, 0, 0, 0, 0, "—", "NaoSocio", 0m,
                new(), new(), new());
        }
    }

    private static string Camel(string s)
        => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private record PointStats(int Inscricoes, int Aves);
}
