namespace AOB.Core.Entities;

public class TransportCarga
{
    public int Id { get; set; }
    public int ConvoyageYearId { get; set; }
    public ConvoyageYear ConvoyageYear { get; set; } = null!;

    // Código T01..Tnn, atribuído por ordem de emissão do planner (sul→norte).
    public string Codigo { get; set; } = "";

    // Nome(s) do responsável — pode ser combinado ("Adriano + Teixeira").
    public string TransportadoraNome { get; set; } = "";

    // Etiqueta de zonas atendidas — pode combinar zonas quando faz merge
    // (ex.: "ALGARVE + LEIRIA/FATIMA").
    public string ZonasLabel { get; set; } = "";

    public int SortOrder { get; set; }
    public string? Notas { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TransportCargaSubmission> Submissoes { get; set; } = [];
}

public class TransportCargaSubmission
{
    public int Id { get; set; }

    public int TransportCargaId { get; set; }
    public TransportCarga TransportCarga { get; set; } = null!;

    public int FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    // Snapshot dos totais no momento do plano — evita recalcular a partir do
    // DataJson só para listar. Actualizado quando a inscrição é editada.
    public int NumAvesConcurso { get; set; }
    public int NumAvesVenda { get; set; }
    // Total físico de aves de transporte (soma das duas direções). Mantido
    // para retrocompatibilidade com o snapshot antigo.
    public int NumAvesTransporte { get; set; }
    // Split por direção do transporte. Guardado separado para o cálculo
    // de ocupação usar max(PtBe, BePt) por criador — as gaiolas que levam
    // aves PT→BE trazem aves BE→PT do mesmo criador (partilham espaço).
    // Se ambos forem 0 e NumAvesTransporte > 0, é um snapshot antigo sem
    // direção conhecida — trata-se como só-de-ida por segurança.
    public int NumAvesTransportePtBe { get; set; }
    public int NumAvesTransporteBePt { get; set; }
}
