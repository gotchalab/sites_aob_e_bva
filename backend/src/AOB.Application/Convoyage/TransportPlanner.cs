namespace AOB.Application.Convoyage;

/// Serviço puro que agrupa inscrições de convoyage em cargas de transporte.
/// Regras:
///   1. Cada carga tem capacidade-alvo (default 20 aves).
///   2. Se um criador tem mais aves do que a capacidade, a submissão divide-se
///      em unidades — preferencialmente aves de concurso numa carga e aves de
///      venda noutra. Se um dos tipos por si só ainda exceder a capacidade, é
///      partido em blocos de tamanho `cap`.
///   3. Zonas (pontos de recolha) são processadas sul→norte por SortOrder.
///   4. Cada carga só transporta criadores da MESMA zona — não há merge entre
///      zonas mesmo quando a última carga da zona fica abaixo do mínimo.
///   5. Por zona: unidades individuais de tamanho >= capacidade enchem cargas
///      próprias. Resto vai a First-Fit Decreasing.
///
/// O nome da transportadora NÃO é atribuído aqui — é o admin que preenche
/// carga a carga na página `/convoyage/{id}/transportes`.
public static class TransportPlanner
{
    // Modo de dimensionamento das cargas.
    //   Total : conta TODAS as aves (concurso + venda + transp PT→BE + transp BE→PT).
    //           Upper bound conservador — o número de "aves ocupadas" pode ser maior
    //           do que o espaço físico realmente necessário no camião, pois uma
    //           gaiola conta como 1 lugar mesmo que vá cheia numa direção e volte
    //           cheia na outra.
    //   PtBe  : só conta as aves que ocupam o camião na IDA (concurso + venda +
    //           transporte de aves que o criador PT VENDE para BE).
    //   BePt  : só conta as aves que ocupam o camião na VOLTA (concurso + venda +
    //           transporte de aves que o criador PT COMPRA em BE).
    //
    // Nota sobre "venda": nem todas as aves de venda são vendidas — as que sobram
    // regressam. Por segurança assumimos que TODAS as aves de venda ocupam espaço
    // em ambos os sentidos. Fase 2 pode marcar "vendida" por ave e libertar espaço
    // na volta.
    public enum PlanMode { Total, PtBe, BePt }

    public record SubmissionInput(
        int SubmissionId,
        string NomeCriador,
        int CollectionPointId,
        string ZonaNome,
        int NumAvesConcurso,
        int NumAvesVenda,
        int NumAvesTransportePtBe = 0,
        int NumAvesTransporteBePt = 0)
    {
        public int NumAvesTransporte => NumAvesTransportePtBe + NumAvesTransporteBePt;
        public int TotalAves => NumAvesConcurso + NumAvesVenda + NumAvesTransporte;

        // Aves que ocupam espaço em cada direção do camião.
        // Venda conta em ambos: as não vendidas regressam (ver nota no enum PlanMode).
        public int AvesPtBe => NumAvesConcurso + NumAvesVenda + NumAvesTransportePtBe;
        public int AvesBePt => NumAvesConcurso + NumAvesVenda + NumAvesTransporteBePt;

        public int AvesParaModo(PlanMode mode) => mode switch
        {
            PlanMode.PtBe => AvesPtBe,
            PlanMode.BePt => AvesBePt,
            _             => TotalAves,
        };
    }

    public record ZoneInput(int CollectionPointId, string Nome, string? Location, int SortOrder)
    {
        public string Label => string.IsNullOrWhiteSpace(Location) ? Nome : $"{Nome} ({Location})";
    }

    public record PlannerConfig(
        int CapacidadePorCarga = 20,
        int MinPorCarga = 16,
        int NumCargasAlvo = 23,
        PlanMode Mode = PlanMode.Total);

    public record CargaPlan(
        string Codigo,
        string TransportadoraNome,
        string ZonasLabel,
        int SortOrder,
        List<CargaSubmissao> Submissoes)
    {
        public int TotalAves => Submissoes.Sum(s => s.NumAvesConcurso + s.NumAvesVenda + s.NumAvesTransporte);
        public int Sobras(int capacidade) => Math.Max(0, capacidade - TotalAves);
    }

    public record CargaSubmissao(
        int SubmissionId, string NomeCriador,
        int NumAvesConcurso, int NumAvesVenda, int NumAvesTransporte = 0);

    public static List<CargaPlan> Plan(
        IEnumerable<SubmissionInput> submissions,
        IEnumerable<ZoneInput> zones,
        PlannerConfig config)
    {
        var subsByZone = submissions
            .GroupBy(s => s.CollectionPointId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var zoneList = zones.OrderBy(z => z.SortOrder).ToList();
        var result = new List<CargaPlan>();

        int seq = 0;
        int NextSeq() => ++seq;

        CargaPlan OpenCarga(string zonaNome)
        {
            var order = NextSeq();
            var carga = new CargaPlan(
                Codigo: $"T{order:00}",
                TransportadoraNome: "",
                ZonasLabel: zonaNome,
                SortOrder: order,
                Submissoes: new());
            result.Add(carga);
            return carga;
        }

        foreach (var zone in zoneList)
        {
            var zoneSubs = subsByZone.TryGetValue(zone.CollectionPointId, out var list)
                ? list : new List<SubmissionInput>();

            // Processa submissão a submissão (maiores primeiro), dando prioridade
            // absoluta a juntar todas as partes do mesmo criador na mesma carga.
            // No modo direccional (PT→BE / BE→PT), quem tem 0 aves nesse sentido
            // é ignorado — não ocupa espaço na carga desse sentido.
            var openCargas = new List<CargaPlan>();

            foreach (var sub in zoneSubs
                .Where(s => s.AvesParaModo(config.Mode) > 0)
                .OrderByDescending(s => s.AvesParaModo(config.Mode)))
            {
                var parts = SplitSubmission(sub, config.CapacidadePorCarga, config.Mode)
                    .OrderByDescending(p => p.Tamanho)
                    .ToList();

                foreach (var part in parts)
                {
                    void Place(CargaPlan c) => c.Submissoes.Add(new CargaSubmissao(
                        part.Sub.SubmissionId, part.NomeCriador,
                        part.NumAvesConcurso, part.NumAvesVenda, part.NumAvesTransporte));

                    // Preferência 1 — carga onde já existe outra parte deste criador.
                    var samecarga = openCargas.FirstOrDefault(c =>
                        c.TotalAves + part.Tamanho <= config.CapacidadePorCarga
                        && c.Submissoes.Any(x => x.SubmissionId == sub.SubmissionId));

                    if (samecarga is not null)
                    {
                        Place(samecarga);
                        continue;
                    }

                    // Preferência 2 — FFD normal na mesma zona.
                    var fitted = openCargas.FirstOrDefault(c =>
                        c.TotalAves + part.Tamanho <= config.CapacidadePorCarga);

                    if (fitted is not null)
                    {
                        Place(fitted);
                        continue;
                    }

                    // Preferência 3 — abrir nova carga (só desta zona).
                    var nova = OpenCarga(zone.Label);
                    Place(nova);
                    openCargas.Add(nova);
                }
            }
        }

        // Consolida partes da mesma submissão que tenham calhado na mesma carga
        // (ex.: 10 concurso + 5 venda do mesmo criador no mesmo transporte).
        // Necessário para não violar o unique (TransportCargaId, FormSubmissionId).
        foreach (var carga in result)
        {
            var grouped = carga.Submissoes
                .GroupBy(s => s.SubmissionId)
                .Select(g => new CargaSubmissao(
                    g.First().SubmissionId,
                    g.First().NomeCriador,
                    g.Sum(x => x.NumAvesConcurso),
                    g.Sum(x => x.NumAvesVenda),
                    g.Sum(x => x.NumAvesTransporte)))
                .ToList();
            if (grouped.Count != carga.Submissoes.Count)
            {
                carga.Submissoes.Clear();
                carga.Submissoes.AddRange(grouped);
            }
        }

        // Renumera T01..Tnn por ordem de emissão (sul→norte).
        for (int i = 0; i < result.Count; i++)
        {
            result[i] = result[i] with
            {
                Codigo = $"T{(i + 1):00}",
                SortOrder = i + 1,
            };
        }

        return result;
    }

    // Se a submissão excede a capacidade, divide-a em unidades preservando a
    // preferência de separar aves de concurso das aves de venda; se um dos tipos
    // por si só ainda exceder `cap`, parte em blocos de tamanho `cap`.
    //
    // Em modos direccionais só se contam as aves que ocupam espaço nesse sentido:
    //   PtBe → concurso + venda + transporte "vende" (PT→BE)
    //   BePt → concurso + venda + transporte "compra" (BE→PT)
    // Concurso e venda contam sempre em ambos sentidos (venda porque as não vendidas
    // regressam — ver nota no enum PlanMode).
    // As aves ignoradas ficam a zero no PackUnit (não são persistidas nesta carga).
    private static IEnumerable<PackUnit> SplitSubmission(SubmissionInput s, int cap, PlanMode mode)
    {
        var contaConcurso = true;                              // sempre — ocupa ambos sentidos
        var contaVenda    = true;                              // sempre — não vendidas regressam
        var contaPtBe  = mode != PlanMode.BePt;                // transporte PT→BE
        var contaBePt  = mode != PlanMode.PtBe;                // transporte BE→PT

        var concurso = contaConcurso ? s.NumAvesConcurso        : 0;
        var venda    = contaVenda    ? s.NumAvesVenda           : 0;
        var trPtBe   = contaPtBe     ? s.NumAvesTransportePtBe  : 0;
        var trBePt   = contaBePt     ? s.NumAvesTransporteBePt  : 0;

        var totalEfectivo = concurso + venda + trPtBe + trBePt;
        if (totalEfectivo == 0) yield break;

        if (totalEfectivo <= cap)
        {
            // Guardamos os componentes de transporte no bucket `NumAvesTransporte`
            // do PackUnit — o snapshot persistido não distingue direção, e no modo
            // direccional só um dos dois tem valor.
            yield return new PackUnit(s.NomeCriador, s,
                concurso, venda, trPtBe + trBePt, totalEfectivo);
            yield break;
        }

        foreach (var part in SplitByType(concurso, cap))
            yield return new PackUnit(s.NomeCriador, s,
                NumAvesConcurso: part, NumAvesVenda: 0, NumAvesTransporte: 0, Tamanho: part);

        foreach (var part in SplitByType(venda, cap))
            yield return new PackUnit(s.NomeCriador, s,
                NumAvesConcurso: 0, NumAvesVenda: part, NumAvesTransporte: 0, Tamanho: part);

        foreach (var part in SplitByType(trPtBe + trBePt, cap))
            yield return new PackUnit(s.NomeCriador, s,
                NumAvesConcurso: 0, NumAvesVenda: 0, NumAvesTransporte: part, Tamanho: part);
    }

    private static IEnumerable<int> SplitByType(int total, int cap)
    {
        if (total <= 0) yield break;
        var remaining = total;
        while (remaining > cap)
        {
            yield return cap;
            remaining -= cap;
        }
        if (remaining > 0) yield return remaining;
    }

    private record PackUnit(
        string NomeCriador,
        SubmissionInput Sub,
        int NumAvesConcurso,
        int NumAvesVenda,
        int NumAvesTransporte,
        int Tamanho);
}
