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
    string Anilha);

public record InscricaoConvoyageRequest(
    string NomeCompleto,
    string Email,
    string? Telefone,
    string Pais,
    string? NumeroSocioBva,
    string? NumeroStam,
    int LocalRecolhaId,
    bool AceitouRegulamento,
    List<AveConvoyageDto> Aves,
    string? TurnstileToken);

public record ConvoyageCollectionPointDto(int Id, string Name, string? Location);

public record ConvoyageActiveYearDto(
    int Id,
    int Year,
    string? Description,
    List<ConvoyageCollectionPointDto> CollectionPoints);
