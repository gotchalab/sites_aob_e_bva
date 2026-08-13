using AOB.Application.Contracts;
using AOB.Application.Forms;
using AOB.Core.Entities;

// Gera um PDF de teste directo do template + overlay, sem BD/API/email.
// Uso: dotnet run --project backend/tools/AOB.PdfTool [--grid] [--calibrate] [-o outfile.pdf]
// Iteração: editar coords em InscricaoSocioPdfGenerator, dotnet build backend/tools/AOB.PdfTool, dotnet run.

var grid = args.Contains("--grid");
var calibrate = args.Contains("--calibrate");
var outIdx = Array.IndexOf(args, "-o");
var outPath = outIdx >= 0 && outIdx + 1 < args.Length
    ? args[outIdx + 1]
    : "d:/PROJETOS/aob/pdf-test.pdf";

if (grid) Environment.SetEnvironmentVariable("PDF_DEBUG_GRID", "1");
else Environment.SetEnvironmentVariable("PDF_DEBUG_GRID", null);
if (calibrate) Environment.SetEnvironmentVariable("PDF_CALIBRATE", "1");
else Environment.SetEnvironmentVariable("PDF_CALIBRATE", null);

var site = new Site
{
    Id = 1,
    Name = "Associação Ornitológica de Barcelos",
    Slug = "aob",
    Domain = "aobarcelos.pt",
    ContactEmail = "aobarcelos@gmail.com",
};

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
    FotoBase64: null,
    AssinaturaBase64: null,
    Notas: null,
    TurnstileToken: null);

var bytes = InscricaoSocioPdfGenerator.Render(site, r, 999);
File.WriteAllBytes(outPath, bytes);
Console.WriteLine($"OK: {outPath} ({bytes.Length} bytes)");
