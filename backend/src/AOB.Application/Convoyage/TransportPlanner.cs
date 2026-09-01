namespace AOB.Application.Convoyage;

/// Serviço puro que agrupa inscrições de convoyage em cargas de transporte.
/// Regras por ordem de prioridade:
///   1. **Cap estrita**: soma de aves numa carga ≤ CapacidadePorCarga.
///      Cada ave física ocupa 1 unidade — sem sharing de gaiolas entre
///      direções (o UI conta aves totais e um valor > cap é erro).
///   2. **Split mínimo por submissão** (regra dominante — tem prioridade
///      sobre clustering de tipo): cada inscrição divide-se em
///      ceil(totalAves / cap) partes. Ex.: 34 aves com cap 20 → 2 partes
///      (20 + 14), NUNCA 3. As partes são preenchidas gulosamente a partir
///      do tipo com maior contagem restante — parte a parte fica o mais
///      "pura" de tipo possível, mas o número de partes é sagrado.
///   3. **Zonas** (pontos de recolha) processadas sul→norte por SortOrder.
///   4. **Multi-zona**: cargas podem servir MÚLTIPLAS zonas para minimizar
///      o número total de transportadoras. Uma carga aberta em "Centro"
///      pode receber aves de "Cantanhede" — o mesmo camião passa nas duas
///      zonas em sequência.
///   5. **Placement**: para cada part já produzido (com número mínimo por
///      submissão fixado), a ordem de escolha da carga é:
///        a) Afinidade de tipo: carga onde já há aves do MESMO tipo
///           dominante do part.
///        b) FFD por ordem de criação (encaixa em cargas de zonas
///           anteriores para aproveitar o mesmo camião).
///        c) Abrir carga nova.
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

        // Ocupação de gaiolas para ESTE criador: concurso e venda ocupam
        // sempre uma gaiola cheia em cada sentido (mesma gaiola dá as duas
        // idas), e o transporte usa max(PtBe, BePt) porque as gaiolas que
        // levam Vende trazem Compra do MESMO criador — não somam.
        public int Ocupacao => NumAvesConcurso + NumAvesVenda
            + Math.Max(NumAvesTransportePtBe, NumAvesTransporteBePt);

        // Total físico de aves (sem sharing) — só para relatórios.
        public int TotalAvesFisicas =>
            NumAvesConcurso + NumAvesVenda + NumAvesTransportePtBe + NumAvesTransporteBePt;

        // Aves que ocupam espaço em cada direção do camião.
        // Venda conta em ambos: as não vendidas regressam (ver nota no enum PlanMode).
        public int AvesPtBe => NumAvesConcurso + NumAvesVenda + NumAvesTransportePtBe;
        public int AvesBePt => NumAvesConcurso + NumAvesVenda + NumAvesTransporteBePt;

        public int AvesParaModo(PlanMode mode) => mode switch
        {
            PlanMode.PtBe => AvesPtBe,
            PlanMode.BePt => AvesBePt,
            _             => Ocupacao,
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
        PlanMode Mode = PlanMode.Total,
        // Se true (default), uma carga aberta em zona anterior pode receber
        // aves de zonas posteriores. Se false, cada zona só usa cargas
        // próprias (comportamento clássico, sem partilha entre zonas).
        bool CruzaZonas = true);

    public record CargaPlan(
        string Codigo,
        string TransportadoraNome,
        string ZonasLabel,
        int SortOrder,
        List<CargaSubmissao> Submissoes)
    {
        // Ocupação efectiva de gaiolas — é isto que compara com CapacidadePorCarga.
        public int Ocupacao => Submissoes.Sum(s => s.Ocupacao);
        public int Sobras(int capacidade) => Math.Max(0, capacidade - Ocupacao);
    }

    public record CargaSubmissao(
        int SubmissionId, string NomeCriador,
        int NumAvesConcurso, int NumAvesVenda,
        int NumAvesTransportePtBe = 0, int NumAvesTransporteBePt = 0)
    {
        public int NumAvesTransporte => NumAvesTransportePtBe + NumAvesTransporteBePt;
        public int Ocupacao => NumAvesConcurso + NumAvesVenda
            + Math.Max(NumAvesTransportePtBe, NumAvesTransporteBePt);
    }

    public static List<CargaPlan> Plan(
        IEnumerable<SubmissionInput> submissions,
        IEnumerable<ZoneInput> zones,
        PlannerConfig config)
    {
        var cap = config.CapacidadePorCarga;

        var subsByZone = submissions
            .GroupBy(s => s.CollectionPointId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var zoneList = zones.OrderBy(z => z.SortOrder).ToList();

        // Rascunhos mutáveis durante o packing; materializados em CargaPlan no
        // fim. Se CruzaZonas=true (default) as cargas vivem entre iterações
        // de zona (uma carga aberta em zona A pode receber aves de B).
        // Se false, `activeDrafts` é limpo a cada zona — cargas antigas
        // ficam preservadas em `sealed` (só para materialização final).
        var drafts = new List<DraftCarga>();          // ordem final
        var activeDrafts = new List<DraftCarga>();    // candidatas para FFD

        foreach (var zone in zoneList)
        {
            if (!config.CruzaZonas) activeDrafts.Clear();

            var zoneSubs = subsByZone.TryGetValue(zone.CollectionPointId, out var list)
                ? list : new List<SubmissionInput>();

            // Achatamos as partes de todas as submissões desta zona e
            // processamos maiores primeiro (FFD clássico). Cada part já
            // é single-type (SplitSubmission divide sempre).
            var parts = zoneSubs
                .Where(s => s.AvesParaModo(config.Mode) > 0)
                .SelectMany(s => SplitSubmission(s, cap, config.Mode))
                .OrderByDescending(p => p.Tamanho)
                .ToList();

            foreach (var part in parts)
            {
                void Place(DraftCarga c)
                {
                    c.Submissoes.Add(new CargaSubmissao(
                        part.Sub.SubmissionId, part.NomeCriador,
                        part.NumAvesConcurso, part.NumAvesVenda,
                        part.NumAvesTransportePtBe, part.NumAvesTransporteBePt));
                    if (!c.Zonas.Contains(zone.Label))
                        c.Zonas.Add(zone.Label);
                }

                // Fit em gaiolas efectivas: para cada criador (incluindo esta
                // submissão se já estiver na carga), soma concurso + venda +
                // max(ptbe, bept). Cada criador partilha as gaiolas dele mas
                // criadores diferentes não partilham entre si.
                bool Fits(DraftCarga c)
                {
                    int total = 0;
                    bool placedSubHere = false;
                    foreach (var g in c.Submissoes.GroupBy(s => s.SubmissionId))
                    {
                        int gc = g.Sum(x => x.NumAvesConcurso);
                        int gv = g.Sum(x => x.NumAvesVenda);
                        int gPtBe = g.Sum(x => x.NumAvesTransportePtBe);
                        int gBePt = g.Sum(x => x.NumAvesTransporteBePt);
                        if (g.Key == part.Sub.SubmissionId)
                        {
                            gc += part.NumAvesConcurso;
                            gv += part.NumAvesVenda;
                            gPtBe += part.NumAvesTransportePtBe;
                            gBePt += part.NumAvesTransporteBePt;
                            placedSubHere = true;
                        }
                        total += gc + gv + Math.Max(gPtBe, gBePt);
                    }
                    if (!placedSubHere)
                    {
                        total += part.NumAvesConcurso + part.NumAvesVenda
                              + Math.Max(part.NumAvesTransportePtBe, part.NumAvesTransporteBePt);
                    }
                    return total <= cap;
                }

                // Preferência 1 — afinidade de tipo dominante: cargas activas
                // onde já há aves do MESMO tipo maioritário deste part
                // (ordenadas por quantidade DESC). Empata pela ordem de criação.
                var byAffinity = activeDrafts
                    .Where(c => Fits(c) && TypeAffinity(c, part) > 0)
                    .OrderByDescending(c => TypeAffinity(c, part))
                    .FirstOrDefault();

                if (byAffinity is not null) { Place(byAffinity); continue; }

                // Preferência 2 — FFD entre cargas activas por ordem de criação.
                var fitted = activeDrafts.FirstOrDefault(Fits);

                if (fitted is not null) { Place(fitted); continue; }

                // Preferência 3 — abrir carga nova.
                var nova = new DraftCarga();
                drafts.Add(nova);
                activeDrafts.Add(nova);
                Place(nova);
            }
        }

        // Reordena cargas para agrupar visualmente splits do mesmo criador —
        // quando uma inscrição aparece em várias cargas, essas cargas ficam
        // consecutivas. Passe estável e greedy: para cada carga na ordem
        // original, insere-a logo a seguir à última carga já reordenada que
        // partilhe algum submissionId. Mantém a ordem sul→norte quando não
        // há conflito, e agrupa splits quando há.
        drafts = ReordenarParaAgruparSplits(drafts);

        // Materializa DraftCarga → CargaPlan, consolidando partes da mesma
        // submissão que tenham calhado na mesma carga (ex.: 10 concurso + 5
        // venda do mesmo criador). Necessário para não violar o unique
        // (TransportCargaId, FormSubmissionId).
        var result = new List<CargaPlan>(drafts.Count);
        for (int i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            var grouped = d.Submissoes
                .GroupBy(s => s.SubmissionId)
                .Select(g => new CargaSubmissao(
                    g.First().SubmissionId,
                    g.First().NomeCriador,
                    g.Sum(x => x.NumAvesConcurso),
                    g.Sum(x => x.NumAvesVenda),
                    g.Sum(x => x.NumAvesTransportePtBe),
                    g.Sum(x => x.NumAvesTransporteBePt)))
                .ToList();

            result.Add(new CargaPlan(
                Codigo: $"T{(i + 1):00}",
                TransportadoraNome: "",
                ZonasLabel: d.Zonas.Count == 0 ? "—" : string.Join(" + ", d.Zonas),
                SortOrder: i + 1,
                Submissoes: grouped));
        }

        return result;
    }

    private static List<DraftCarga> ReordenarParaAgruparSplits(List<DraftCarga> drafts)
    {
        var reordered = new List<DraftCarga>(drafts.Count);
        foreach (var carga in drafts)
        {
            var subIds = new HashSet<int>(carga.Submissoes.Select(s => s.SubmissionId));

            // Encontra a última carga já reordenada que partilhe algum criador
            // — a nova entra logo a seguir para ficarem consecutivas.
            int insertAt = reordered.Count;   // default: append
            for (int i = reordered.Count - 1; i >= 0; i--)
            {
                if (reordered[i].Submissoes.Any(s => subIds.Contains(s.SubmissionId)))
                {
                    insertAt = i + 1;
                    break;
                }
            }
            reordered.Insert(insertAt, carga);
        }
        return reordered;
    }

    // Peso da carga para este part por afinidade do tipo DOMINANTE do part.
    // Se o part é misto (ex.: 4c + 10v), o tipo dominante é aquele com mais
    // aves — desempatam-se por concurso > venda > transporte.
    private static int TypeAffinity(DraftCarga c, PackUnit p)
    {
        var trTotal = p.NumAvesTransportePtBe + p.NumAvesTransporteBePt;
        // Escolhe o tipo dominante do part.
        if (p.NumAvesConcurso >= p.NumAvesVenda && p.NumAvesConcurso >= trTotal && p.NumAvesConcurso > 0)
            return c.Submissoes.Sum(s => s.NumAvesConcurso);
        if (p.NumAvesVenda >= trTotal && p.NumAvesVenda > 0)
            return c.Submissoes.Sum(s => s.NumAvesVenda);
        if (trTotal > 0)
            return c.Submissoes.Sum(s => s.NumAvesTransporte);
        return 0;
    }

    // Estrutura mutável usada durante o packing. Convertida em CargaPlan
    // (record imutável) só depois de todas as zonas terem sido processadas.
    private sealed class DraftCarga
    {
        public List<string> Zonas { get; } = new();
        public List<CargaSubmissao> Submissoes { get; } = new();
    }

    // Divide a submissão no NÚMERO MÍNIMO de partes (ceil(ocupacao/cap)) —
    // regra dominante para não fragmentar criadores por muitas cargas. Cada
    // parte é preenchida gulosamente: primeiro c+v (que contam 1 por ave em
    // ambos os sentidos), depois transporte emparelhado PtBe↔BePt (cada par
    // usa 1 gaiola, pois o mesmo lugar carrega Vende à ida e Compra à volta),
    // e por fim o resíduo unidireccional.
    //
    // Em modos direccionais só contam as aves que ocupam espaço nesse sentido.
    private static IEnumerable<PackUnit> SplitSubmission(SubmissionInput s, int cap, PlanMode mode)
    {
        var contaPtBe = mode != PlanMode.BePt;
        var contaBePt = mode != PlanMode.PtBe;

        int remC     = s.NumAvesConcurso;
        int remV     = s.NumAvesVenda;
        int remPtBe  = contaPtBe ? s.NumAvesTransportePtBe : 0;
        int remBePt  = contaBePt ? s.NumAvesTransporteBePt : 0;

        // Ocupação total (com sharing intra-criador) — é isto que conta para cap.
        int ocupTotal = remC + remV + Math.Max(remPtBe, remBePt);
        if (ocupTotal <= 0) yield break;

        while (remC + remV + remPtBe + remBePt > 0)
        {
            int need = cap;   // gaiolas efectivas ainda por preencher nesta parte
            int takeC = 0, takeV = 0, takePtBe = 0, takeBePt = 0;

            // Fase 1 — enche a parte com c+v pelo maior bucket restante,
            // para manter homogeneidade de tipo.
            while (need > 0 && (remC > 0 || remV > 0))
            {
                if (remC >= remV && remC > 0)
                { int t = Math.Min(remC, need); takeC += t; remC -= t; need -= t; }
                else if (remV > 0)
                { int t = Math.Min(remV, need); takeV += t; remV -= t; need -= t; }
            }

            // Fase 2 — pares Vende↔Compra do mesmo criador. Cada par usa 1
            // gaiola (o lugar leva Vende à ida e traz Compra à volta).
            if (need > 0 && remPtBe > 0 && remBePt > 0)
            {
                int pairs = Math.Min(Math.Min(remPtBe, remBePt), need);
                takePtBe += pairs; takeBePt += pairs;
                remPtBe -= pairs;  remBePt -= pairs;
                need -= pairs;
            }

            // Fase 3 — resíduo unidireccional (só uma direção com aves).
            if (need > 0 && remPtBe > 0)
            {
                int t = Math.Min(remPtBe, need);
                takePtBe += t; remPtBe -= t; need -= t;
            }
            if (need > 0 && remBePt > 0)
            {
                int t = Math.Min(remBePt, need);
                takeBePt += t; remBePt -= t; need -= t;
            }

            int ocup = takeC + takeV + Math.Max(takePtBe, takeBePt);
            if (ocup <= 0) yield break;   // salvaguarda

            yield return new PackUnit(s.NomeCriador, s,
                NumAvesConcurso: takeC, NumAvesVenda: takeV,
                NumAvesTransportePtBe: takePtBe, NumAvesTransporteBePt: takeBePt,
                Tamanho: ocup);
        }
    }

    private record PackUnit(
        string NomeCriador,
        SubmissionInput Sub,
        int NumAvesConcurso,
        int NumAvesVenda,
        int NumAvesTransportePtBe,
        int NumAvesTransporteBePt,
        int Tamanho);
}
