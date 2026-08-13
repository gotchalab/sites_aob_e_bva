namespace AOB.Application.Contracts;

public record SocioProfileDto(
    int Id,
    string NumeroSocio,
    string NomeCompleto,
    string Email,
    string? Telefone,
    string? NIF,
    DateTime? DataNascimento,
    string? Morada,
    string? CodigoPostal,
    string? Localidade,
    string? FotoPath,
    List<string> EspeciesInteresse,
    DateTime DataInscricao,
    string Estado);

public record UpdateSocioProfileRequest(
    string NomeCompleto,
    string? Telefone,
    string? NIF,
    DateTime? DataNascimento,
    string? Morada,
    string? CodigoPostal,
    string? Localidade,
    List<string>? EspeciesInteresse);

public record QuotaDto(int Id, int Ano, decimal Valor, DateTime? DataPagamento, string? Metodo, string? Recibo);

public record PedidoAnilhaDto(
    int Id, string EspecieCientifica, string? EspecieNomeComum,
    int Ano, decimal Diametro, int Quantidade,
    string Estado, DateTime DataPedido, DateTime? DataEntrega, string? Observacoes);

public record CreatePedidoAnilhaRequest(
    string EspecieCientifica,
    string? EspecieNomeComum,
    int Ano,
    decimal Diametro,
    int Quantidade,
    string? Observacoes);
