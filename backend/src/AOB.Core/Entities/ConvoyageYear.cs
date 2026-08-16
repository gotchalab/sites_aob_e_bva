namespace AOB.Core.Entities;

public class ConvoyageYear
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public int Year { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Data/hora (UTC) de fecho das inscrições. Se estiver definida e já passou,
    // o formulário público bloqueia novas submissões.
    public DateTime? RegistrationClosesAt { get; set; }

    // Id do Download que serve de regulamento deste ano. O API expoe o path
    // publico em ConvoyageActiveYearDto.RegulamentoUrl para o form publico
    // linkar no checkbox "Li e aceito o regulamento".
    public int? RegulamentoDownloadId { get; set; }
    public Download? RegulamentoDownload { get; set; }

    // Planeamento de transporte (F2)
    public int NumCargasAlvo { get; set; } = 23;
    public int CapacidadePorCarga { get; set; } = 20;
    public int MinPorCarga { get; set; } = 16;
    // JSON: { "ZONA": ["Nome1","Nome2"], ... } — round-robin por zona.
    public string TransportadorasJson { get; set; } = "{}";

    // Tarifas e taxas do ano (editáveis no backoffice; alimentam form/Excel/PDF).
    public decimal PrecoInscricao            { get; set; } = 8.00m;
    public decimal PrecoAveBva               { get; set; } = 3.00m;
    public decimal PrecoGaiola               { get; set; } = 3.00m;
    public decimal TarifaTransporteSocio     { get; set; } = 5.50m;
    public decimal TarifaTransporteNaoSocio  { get; set; } = 15.50m;
    public decimal TarifaAdquirenteSocio     { get; set; } = 15.50m;
    public decimal TarifaAdquirenteNaoSocio  { get; set; } = 20.50m;
    public decimal Quota                     { get; set; } = 40.00m;

    public ICollection<ConvoyageCollectionPoint> CollectionPoints { get; set; } = [];
    public ICollection<FormSubmission> Submissions { get; set; } = [];
    public ICollection<NomenclatureGroup> NomenclatureGroups { get; set; } = [];
    public ICollection<TransportCarga> TransportCargas { get; set; } = [];
}
