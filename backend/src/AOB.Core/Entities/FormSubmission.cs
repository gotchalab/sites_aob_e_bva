namespace AOB.Core.Entities;

public class FormSubmission
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public FormType FormType { get; set; }
    public string DataJson { get; set; } = "{}";
    public FormStatus Status { get; set; } = FormStatus.Pending;

    public string? HandledById { get; set; }
    public ApplicationUser? HandledBy { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? Notes { get; set; }

    public int? ConvoyageYearId { get; set; }
    public ConvoyageYear? ConvoyageYear { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public enum FormType
{
    Contact = 0,
    InscricaoSocio = 1,
    PedidoAnilhas = 2,
    InscricaoConvoyage = 3,
    Other = 99,
}

public enum FormStatus
{
    Pending = 0,
    InProgress = 1,
    Handled = 2,
    Rejected = 3,
    Spam = 4,
}
