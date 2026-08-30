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

public record FormSubmissionResponse(
    bool Ok,
    string? Error = null,
    int? SubmissionId = null,
    string? DownloadToken = null,
    // True se o ano da inscrição já tinha Campeonato + Matrícula TRACES + a
    // assinatura foi persistida — nesse caso o TRACES é oferecido na página
    // de agradecimento em paralelo com o PDF de inscrição habitual.
    bool TracesAvailable = false);

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
    string? TurnstileToken,
    string? Morada = null,
    string? CodigoPostal = null,
    string? Localidade = null,
    string? AssinaturaPngBase64 = null,
    bool DeclaraArt59 = false)
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

    // Número de espaços de transporte de aves adquiridas/cedidas. Um espaço é
    // uma gaiola no camião que pode ir (PT→BE), voltar (BE→PT) ou ambos —
    // portanto o custo é max(nº aves compra, nº aves vende), não a soma.
    public static int EspacosTransporteAdquirido(int numAvesCompra, int numAvesVende) =>
        Math.Max(Math.Max(0, numAvesCompra), Math.Max(0, numAvesVende));

    // numAvesVendaOferecidas: aves de venda isentas de pagamento (oferecidas
    // pelo criador). Continuam a existir na inscrição (e a ocupar espaço na
    // sala de vendas / camião), mas são descontadas dos custos de gaiolas e
    // transporte de exposição.
    //
    // numAvesTransporteOferecidas: NÃO é o nº de aves oferecidas — é o nº de
    // *espaços* isentos. Cada espaço isento subtrai uma linha à factura, mas
    // o espaço físico continua a existir. Semanticamente equivalente a "N
    // gaiolas oferecidas ao criador".
    //
    // numAvesTransporteCompra / numAvesTransporteVende: nº de aves por sentido.
    // Se ambos passados, os espaços = max(compra, vende). Se algum for null,
    // fallback ao comportamento antigo (numAvesTransporte = nº espaços) para
    // compat com chamadores que não têm breakdown.
    //
    // A exposição fixa mantém-se enquanto houver aves físicas de
    // concurso/venda (mesmo que todas sejam oferecidas).
    public static (decimal fixa, decimal inscricoes, decimal gaiolas, decimal transporte, decimal transporteAdquiridas, decimal quota, decimal total)
        Compute(int numAvesConcurso, int numAvesVenda, int numAvesTransporte, SocioBvaStatus status,
            int numAvesVendaOferecidas = 0, int numAvesTransporteOferecidas = 0,
            int? numAvesTransporteCompra = null, int? numAvesTransporteVende = null)
    {
        var socioBva = status != SocioBvaStatus.NaoSocio;
        var transporte = TransportePorAve(socioBva);
        var transporteAdquiridaUnit = TransporteAdquiridaPorAve(socioBva);
        var vendaFaturavel = Math.Max(0, numAvesVenda - Math.Max(0, numAvesVendaOferecidas));
        var espacosTotais = (numAvesTransporteCompra is int c && numAvesTransporteVende is int v)
            ? EspacosTransporteAdquirido(c, v)
            : Math.Max(0, numAvesTransporte);
        var espacosFaturaveis = Math.Max(0, espacosTotais - Math.Max(0, numAvesTransporteOferecidas));
        var totalAvesConcursoVendaFaturavel = numAvesConcurso + vendaFaturavel;
        // Exposição fixa aplica-se enquanto houver aves físicas (mesmo que todas as
        // pagas sejam concurso; se só houver aves oferecidas, também há exposição).
        var temExposicao = (numAvesConcurso + numAvesVenda) > 0;
        var fixa = temExposicao ? InscricaoExposicao : 0m;
        var inscricoes = InscricaoPorAve * numAvesConcurso;
        var gaiolas = GaiolaPorAve * totalAvesConcursoVendaFaturavel;
        var transporteTotal = transporte * totalAvesConcursoVendaFaturavel;
        var transporteAdquiridas = transporteAdquiridaUnit * espacosFaturaveis;
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
    DateTime? RegistrationClosesAt,
    // URL publica do PDF/documento regulamento (Download.StoragePath). Null se
    // nao houver regulamento configurado no ano — o frontend mostra o checkbox
    // sem link nesse caso.
    string? RegulamentoUrl,
    string? RegulamentoFileName);
