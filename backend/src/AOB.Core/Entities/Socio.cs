namespace AOB.Core.Entities;

public class Socio
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string NumeroSocio { get; set; } = "";
    public string NomeCompleto { get; set; } = "";
    public string? NIF { get; set; }
    public DateTime? DataNascimento { get; set; }
    public string? Morada { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Localidade { get; set; }
    public string? Telefone { get; set; }
    public string Email { get; set; } = "";
    public string? FotoPath { get; set; }
    public List<string> EspeciesInteresse { get; set; } = new();

    public DateTime DataInscricao { get; set; } = DateTime.UtcNow;
    public SocioEstado Estado { get; set; } = SocioEstado.Ativo;
    public string? Notas { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Quota> Quotas { get; set; } = new List<Quota>();
    public ICollection<PedidoAnilha> Pedidos { get; set; } = new List<PedidoAnilha>();
}

public enum SocioEstado
{
    Ativo = 0,
    Suspenso = 1,
    Baixa = 2,
}

public class Quota
{
    public int Id { get; set; }
    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;

    public int Ano { get; set; }
    public decimal Valor { get; set; }
    public DateTime? DataPagamento { get; set; }
    public string? Metodo { get; set; }
    public string? Recibo { get; set; }
    public string? Notas { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PedidoAnilha
{
    public int Id { get; set; }
    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;

    public string EspecieCientifica { get; set; } = "";
    public string? EspecieNomeComum { get; set; }
    public int Ano { get; set; }
    public decimal Diametro { get; set; }
    public int Quantidade { get; set; }
    public string? Observacoes { get; set; }

    public PedidoAnilhaEstado Estado { get; set; } = PedidoAnilhaEstado.Pendente;
    public DateTime DataPedido { get; set; } = DateTime.UtcNow;
    public DateTime? DataEntrega { get; set; }
    public string? Notas { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PedidoAnilhaEstado
{
    Pendente = 0,
    Aprovado = 1,
    Encomendado = 2,
    Entregue = 3,
    Cancelado = 4,
}
