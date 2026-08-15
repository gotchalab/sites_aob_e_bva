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
    public record SubmissionInput(
        int SubmissionId,
        string NomeCriador,
        int CollectionPointId,
        string ZonaNome,
        int NumAvesConcurso,
        int NumAvesVenda,
        int NumAvesTransporte = 0)
    {
        public int TotalAves => NumAvesConcurso + NumAvesVenda + NumAvesTransporte;
    }

    public record ZoneInput(int CollectionPointId, string Nome, string? Location, int SortOrder)
    {
        public string Label => string.IsNullOrWhiteSpace(Location) ? Nome : $"{Nome} ({Location})";
    }

    public record PlannerConfig(
        int CapacidadePorCarga = 20,
        int MinPorCarga = 16,
        int NumCargasAlvo = 23);

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
            var openCargas = new List<CargaPlan>();

            foreach (var sub in zoneSubs.OrderByDescending(s => s.TotalAves))
            {
                var parts = SplitSubmission(sub, config.CapacidadePorCarga)
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
    private static IEnumerable<PackUnit> SplitSubmission(SubmissionInput s, int cap)
    {
        if (s.TotalAves <= cap)
        {
            yield return new PackUnit(s.NomeCriador, s,
                s.NumAvesConcurso, s.NumAvesVenda, s.NumAvesTransporte, s.TotalAves);
            yield break;
        }

        foreach (var part in SplitByType(s.NumAvesConcurso, cap))
            yield return new PackUnit(s.NomeCriador, s,
                NumAvesConcurso: part, NumAvesVenda: 0, NumAvesTransporte: 0, Tamanho: part);

        foreach (var part in SplitByType(s.NumAvesVenda, cap))
            yield return new PackUnit(s.NomeCriador, s,
                NumAvesConcurso: 0, NumAvesVenda: part, NumAvesTransporte: 0, Tamanho: part);

        foreach (var part in SplitByType(s.NumAvesTransporte, cap))
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
