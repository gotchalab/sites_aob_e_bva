using System.Text.Json.Serialization;

namespace AOB.Application.Contracts;

public record ContactRequest(
    string Name,
    string Email,
    string? Phone,
    string Subject,
    string Message,
    string? TurnstileToken);

[JsonConverter(typeof(JsonStringEnumConverter<EstadoCivilOpt>))]
public enum EstadoCivilOpt
{
    Solteiro = 0,
    Casado = 1,
    Divorciado = 2,
    Viuvo = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter<StamStatus>))]
public enum StamStatus
{
    Nao = 0,
    Sim = 1,
    QueroTer = 2,
}

public record InscricaoSocioRequest(
    string NomeCompleto,
    string Email,
    string? Telefone,
    string? CartaoCidadao,
    string? NIF,
    string? Nacionalidade,
    DateTime? DataNascimento,
    EstadoCivilOpt? EstadoCivil,
    string? Morada,
    string? MoradaLinha2,
    string? CodigoPostal,
    string? Localidade,
    string? Profissao,
    bool SocioApoiante,
    bool SocioCriador,
    StamStatus? StamFonp,
    string? StamFonpNumero,
    bool SocioBvaPortugal,
    StamStatus? StamBva,
    string? StamBvaNumero,
    bool AceitouRegulamento,
    string? Notas,
    string? TurnstileToken);

public record FormSubmissionResponse(bool Ok, string? Error = null, int? SubmissionId = null);

public record AveConvoyageDto(
    string Serie,
    string EspecieMutacao,
    string Especie,
    string TipoClasse,
    string Anilha,
    Guid? EquipaId = null,
    string? PosicaoEquipa = null);

[JsonConverter(typeof(JsonStringEnumConverter<SexoAve>))]
public enum SexoAve
{
    Macho = 0,
    Femea = 1,
    Indefinido = 2,
}

public record AveVendaDto(
    string Especie,
    string? TipoClasse,
    string EspecieMutacao,
    bool EspecieLivre,
    string? DataNascimento,
    SexoAve Sexo,
    decimal Preco,
    string Anilha);

[JsonConverter(typeof(JsonStringEnumConverter<OrigemAveTransporte>))]
public enum OrigemAveTransporte
{
    Compra = 0,
    Vende = 1,
}

public record AveTransporteDto(
    string Especie,
    OrigemAveTransporte Origem,
    string Anilha,
    string DestinatarioNome,
    string DestinatarioWhatsapp,
    string? DestinatarioNotas = null);

[JsonConverter(typeof(JsonStringEnumConverter<SocioBvaStatus>))]
public enum SocioBvaStatus
{
    JaSocio = 0,
    PagaComInscricao = 1,
    NaoSocio = 2,
}

public record InscricaoConvoyageRequest(
    string NomeCompleto,
    string Email,
    string? Telefone,
    string Pais,
    string? NumeroStam,
    int LocalRecolhaId,
    bool AceitouRegulamento,
    SocioBvaStatus SocioBvaStatus,
    List<AveConvoyageDto> Aves,
    List<AveVendaDto>? AvesVenda,
    List<AveTransporteDto>? AvesTransporte,
    string? TurnstileToken)
{
    public bool SocioBva => SocioBvaStatus != SocioBvaStatus.NaoSocio;
}

public static class ConvoyagePricing
{
    public const decimal InscricaoExposicao = 8.00m;
    public const decimal InscricaoPorAve = 3.00m;
    public const decimal GaiolaPorAve = 3.00m;
    public const decimal TransporteSocio = 5.50m;
    public const decimal TransporteNaoSocio = 15.50m;
    public const decimal TransporteAdquiridaSocio = 15.50m;
    public const decimal TransporteAdquiridaNaoSocio = 20.50m;
    public const decimal QuotaBva = 40.00m;

    public static decimal TransportePorAve(bool socioBva) => socioBva ? TransporteSocio : TransporteNaoSocio;

    public static decimal TransporteAdquiridaPorAve(bool socioBva) =>
        socioBva ? TransporteAdquiridaSocio : TransporteAdquiridaNaoSocio;

    public static (decimal fixa, decimal inscricoes, decimal gaiolas, decimal transporte, decimal transporteAdquiridas, decimal quota, decimal total)
        Compute(int numAvesConcurso, int numAvesVenda, int numAvesTransporte, SocioBvaStatus status)
    {
        var socioBva = status != SocioBvaStatus.NaoSocio;
        var transporte = TransportePorAve(socioBva);
        var transporteAdquiridaUnit = TransporteAdquiridaPorAve(socioBva);
        var totalAvesConcursoVenda = numAvesConcurso + numAvesVenda;
        var temExposicao = totalAvesConcursoVenda > 0;
        var fixa = temExposicao ? InscricaoExposicao : 0m;
        var inscricoes = InscricaoPorAve * numAvesConcurso;
        var gaiolas = GaiolaPorAve * totalAvesConcursoVenda;
        var transporteTotal = transporte * totalAvesConcursoVenda;
        var transporteAdquiridas = transporteAdquiridaUnit * numAvesTransporte;
        var quota = status == SocioBvaStatus.PagaComInscricao ? QuotaBva : 0m;
        var total = fixa + inscricoes + gaiolas + transporteTotal + transporteAdquiridas + quota;
        return (fixa, inscricoes, gaiolas, transporteTotal, transporteAdquiridas, quota, total);
    }
}

public record ConvoyageCollectionPointDto(int Id, string Name, string? Location);

public record ConvoyageActiveYearDto(
    int Id,
    int Year,
    string? Description,
    List<ConvoyageCollectionPointDto> CollectionPoints,
    DateTime? RegistrationClosesAt);
