using AOB.Application.Contracts;
using AOB.Application.Forms;
using AOB.Core.Entities;

// Gera um PDF de teste directo do template + overlay, sem BD/API/email.
// Uso:
//   dotnet run --project backend/tools/AOB.PdfTool [--grid] [--calibrate] [-o outfile.pdf]
//   dotnet run --project backend/tools/AOB.PdfTool --convoyage [-o outfile.pdf]
// Iteração: editar coords em InscricaoSocioPdfGenerator/InscricaoConvoyagePdfGenerator,
//           dotnet build backend/tools/AOB.PdfTool, dotnet run.

var grid = args.Contains("--grid");
var calibrate = args.Contains("--calibrate");
var convoyage = args.Contains("--convoyage");
var outIdx = Array.IndexOf(args, "-o");
var outPath = outIdx >= 0 && outIdx + 1 < args.Length
    ? args[outIdx + 1]
    : (convoyage ? "d:/PROJETOS/aob/pdf-convoyage-test.pdf" : "d:/PROJETOS/aob/pdf-test.pdf");

if (grid) Environment.SetEnvironmentVariable("PDF_DEBUG_GRID", "1");
else Environment.SetEnvironmentVariable("PDF_DEBUG_GRID", null);
if (calibrate) Environment.SetEnvironmentVariable("PDF_CALIBRATE", "1");
else Environment.SetEnvironmentVariable("PDF_CALIBRATE", null);

var site = new Site
{
    Id = 2,
    Name = "BVA Portugal",
    Slug = "bva-portugal",
    Domain = "bvaportugal.pt",
    ContactEmail = "bvaportugal@gmail.com",
};

if (convoyage)
{
    var req = new InscricaoConvoyageRequest(
        NomeCompleto: "Bruno Vale teste",
        Email: "teste@example.com",
        Telefone: "+351967332859",
        Pais: "Portugal",
        NumeroStam: null,
        LocalRecolhaId: 1,
        AceitouRegulamento: true,
        SocioBvaStatus: SocioBvaStatus.JaSocio,
        Aves: new()
        {
            new AveConvoyageDto(Serie: "002/01", EspecieMutacao: "DD green",
                Especie: "Roseicollis", TipoClasse: "Ind", Anilha: "321654987321654987 3 323 232"),
        },
        AvesVenda: new()
        {
            new AveVendaDto(Especie: "Roseicollis", TipoClasse: "Ind",
                EspecieMutacao: "DD green", EspecieLivre: false,
                DataNascimento: "2026", Sexo: SexoAve.Macho, Preco: 23.00m,
                Anilha: "321654987321654987 3 323 232"),
            new AveVendaDto(Especie: "Roseicollis", TipoClasse: "Ind",
                EspecieMutacao: "D green", EspecieLivre: false,
                DataNascimento: "2025", Sexo: SexoAve.Femea, Preco: 25.50m,
                Anilha: "AOB PT 384P 001 FNP 5.0 2"),
        },
        AvesTransporte: new()
        {
            new AveTransporteDto(Especie: "Personatus",
                Origem: OrigemAveTransporte.Vende,
                Anilha: "321654987321654987 2",
                DestinatarioNome: "Bruno Vale 3 transporte",
                DestinatarioWhatsapp: "123 123 123",
                DestinatarioNotas: "notas para entrega"),
        },
        TurnstileToken: null);

    var bytes = InscricaoConvoyagePdfGenerator.Render(site, req, submissionId: 999,
        localRecolha: "Loja Conceito Animal (Barcelos)", year: 2026);
    File.WriteAllBytes(outPath, bytes);
    Console.WriteLine($"OK: {outPath} ({bytes.Length} bytes)");
    RenderPng(bytes, Path.ChangeExtension(outPath, ".png"));
    return;
}

var r = new InscricaoSocioRequest(
    NomeCompleto: "TESTE Nome Completo",
    Email: "teste@example.com",
    Telefone: "912345678",
    CartaoCidadao: "12345678",
    NIF: "123456789",
    Nacionalidade: "Portuguesa",
    DataNascimento: new DateTime(1985, 3, 15),
    EstadoCivil: EstadoCivilOpt.Casado,
    Morada: "Rua da Ornitologia, 42",
    MoradaLinha2: "Bairro dos Passaros",
    CodigoPostal: "4750-134",
    Localidade: "Arcozelo",
    Profissao: "Engenheiro",
    SocioApoiante: true,
    SocioCriador: true,
    StamFonp: StamStatus.Sim,
    StamFonpNumero: "F1234",
    SocioBvaPortugal: true,
    StamBva: StamStatus.Sim,
    StamBvaNumero: "B5678",
    AceitouRegulamento: true,
    Notas: null,
    TurnstileToken: null);

var socioBytes = InscricaoSocioPdfGenerator.Render(site, r, 999);
File.WriteAllBytes(outPath, socioBytes);
Console.WriteLine($"OK: {outPath} ({socioBytes.Length} bytes)");
RenderPng(socioBytes, Path.ChangeExtension(outPath, ".png"));
return;

static void RenderPng(byte[] pdf, string outPng)
{
    try
    {
        var pages = PDFtoImage.Conversion.ToImages(pdf, options: new PDFtoImage.RenderOptions(Dpi: 120)).ToArray();
        for (int i = 0; i < pages.Length; i++)
        {
            var pagePath = pages.Length == 1
                ? outPng
                : Path.Combine(Path.GetDirectoryName(outPng)!,
                    $"{Path.GetFileNameWithoutExtension(outPng)}-p{i + 1}.png");
            using var fs = File.Create(pagePath);
            pages[i].Encode(fs, SkiaSharp.SKEncodedImageFormat.Png, 100);
            Console.WriteLine($"PNG: {pagePath}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"PNG render falhou: {ex.Message}");
    }
}
