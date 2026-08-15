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
    public int NumAvesTransporte { get; set; }
}
