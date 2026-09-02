using AOB.Application.Contracts;
using AOB.Application.Convoyage;
using AOB.Application.Forms;
using AOB.Core.Entities;

// Gera um PDF de teste directo do template + overlay, sem BD/API/email.
// Uso:
//   dotnet run --project backend/tools/AOB.PdfTool [--grid] [--calibrate] [-o outfile.pdf]
//   dotnet run --project backend/tools/AOB.PdfTool --convoyage [-o outfile.pdf]
//   dotnet run --project backend/tools/AOB.PdfTool --etiquetas [-o outfile.pdf]
// Iteração: editar coords em InscricaoSocioPdfGenerator/InscricaoConvoyagePdfGenerator,
//           dotnet build backend/tools/AOB.PdfTool, dotnet run.

var grid = args.Contains("--grid");
var calibrate = args.Contains("--calibrate");
var convoyage = args.Contains("--convoyage");
var etiquetas = args.Contains("--etiquetas");

// --render-pdf-only <input.pdf> -o <output.png>: só renderiza um PDF já existente
// para PNG (mesma DPI dos outros outputs). Útil para diffing lado-a-lado.
var renderIdx = Array.IndexOf(args, "--render-pdf-only");
if (renderIdx >= 0 && renderIdx + 1 < args.Length)
{
    var inputPdf = args[renderIdx + 1];
    var outIdxR = Array.IndexOf(args, "-o");
    var outPngPath = outIdxR >= 0 && outIdxR + 1 < args.Length
        ? args[outIdxR + 1]
        : Path.ChangeExtension(inputPdf, ".png");
    var pdfBytes = File.ReadAllBytes(inputPdf);
    RenderPng(pdfBytes, outPngPath);
    return;
}

// --diff-png <a.png> <b.png> -o <out.png>: overlay pixel-a-pixel para comparar
// posições. Vermelho = só em A; Azul = só em B; Cinza = em ambos.
var diffIdx = Array.IndexOf(args, "--diff-png");
if (diffIdx >= 0 && diffIdx + 2 < args.Length)
{
    var pathA = args[diffIdx + 1];
    var pathB = args[diffIdx + 2];
    var outIdxD = Array.IndexOf(args, "-o");
    var outDiff = outIdxD >= 0 && outIdxD + 1 < args.Length
        ? args[outIdxD + 1]
        : "diff.png";
    using var bmpA = SkiaSharp.SKBitmap.Decode(pathA);
    using var bmpB = SkiaSharp.SKBitmap.Decode(pathB);
    var w = Math.Min(bmpA.Width, bmpB.Width);
    var h = Math.Min(bmpA.Height, bmpB.Height);
    using var diff = new SkiaSharp.SKBitmap(w, h);
    int onlyA = 0, onlyB = 0, both = 0, none = 0;
    for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
    {
        var pa = bmpA.GetPixel(x, y);
        var pb = bmpB.GetPixel(x, y);
        bool aInk = pa.Red < 240 || pa.Green < 240 || pa.Blue < 240;
        bool bInk = pb.Red < 240 || pb.Green < 240 || pb.Blue < 240;
        if (aInk && bInk) { diff.SetPixel(x, y, new SkiaSharp.SKColor(180, 180, 180)); both++; }
        else if (aInk)    { diff.SetPixel(x, y, new SkiaSharp.SKColor(220, 0, 0)); onlyA++; }
        else if (bInk)    { diff.SetPixel(x, y, new SkiaSharp.SKColor(0, 100, 220)); onlyB++; }
        else              { diff.SetPixel(x, y, SkiaSharp.SKColors.White); none++; }
    }
    using var img = SkiaSharp.SKImage.FromBitmap(diff);
    using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
    using var fs = File.Create(outDiff);
    data.SaveTo(fs);
    var total = w * h;
    Console.WriteLine($"diff → {outDiff}");
    Console.WriteLine($"  só A (vermelho):  {onlyA,10:N0} px ({100.0 * onlyA / total:F3}%)");
    Console.WriteLine($"  só B (azul):      {onlyB,10:N0} px ({100.0 * onlyB / total:F3}%)");
    Console.WriteLine($"  ambos (cinza):    {both,10:N0} px ({100.0 * both / total:F3}%)");
    return;
}
var outIdx = Array.IndexOf(args, "-o");
var outPath = outIdx >= 0 && outIdx + 1 < args.Length
    ? args[outIdx + 1]
    : etiquetas ? "d:/PROJETOS/aob/pdf-etiquetas-test.pdf"
    : convoyage ? "d:/PROJETOS/aob/pdf-convoyage-test.pdf"
    : "d:/PROJETOS/aob/pdf-test.pdf";

if (grid) Environment.SetEnvironmentVariable("PDF_DEBUG_GRID", "1");
else Environment.SetEnvironmentVariable("PDF_DEBUG_GRID", null);
if (calibrate) Environment.SetEnvironmentVariable("PDF_CALIBRATE", "1");
else Environment.SetEnvironmentVariable("PDF_CALIBRATE", null);

if (etiquetas)
{
    // Réplica sintética do sample _local/examples/etiquetas_PACOS_DE_FERREIRA_avery3421_25.4mm.pdf:
    // 4 criadores, mix de aves concurso/venda/transporte, para validar layout.
    var labels = new List<EtiquetasAvery3421PdfGenerator.EtiquetaLabel>();

    void AddConcurso(string nome, string serie, string desc, string anilha)
        => labels.Add(new EtiquetasAvery3421PdfGenerator.EtiquetaLabel(
            nome, anilha, desc, serie, EtiquetasAvery3421PdfGenerator.EtiquetaTipo.Concurso));

    void AddVenda(string nome, string desc, string anilha)
        => labels.Add(new EtiquetasAvery3421PdfGenerator.EtiquetaLabel(
            nome, anilha, desc, "VENDAS", EtiquetasAvery3421PdfGenerator.EtiquetaTipo.Venda));

    void AddTransporte(string nome, string destinatario, string anilha)
        => labels.Add(new EtiquetasAvery3421PdfGenerator.EtiquetaLabel(
            nome, anilha, $"Entregar a: {destinatario}", "TRANSPORTE",
            EtiquetasAvery3421PdfGenerator.EtiquetaTipo.Transporte));

    // Filipe Lopes — 12 concurso + 14 transporte
    AddConcurso("Filipe Lopes", "003/04", "OPALINE VIOLET",         "237O 143 FNP23 5.0 SOG");
    AddConcurso("Filipe Lopes", "011/13", "OPALINE-CINNAMON BLUE",  "237O 138 FNP23 5.0 SOG");
    AddConcurso("Filipe Lopes", "011/04", "CINNAMON BLUE",          "237O 058 FNP24 5.0 S0G");
    AddConcurso("Filipe Lopes", "012/04", "PALLID BLUE",            "237O 127 FNP24 5.0 SOG");
    AddConcurso("Filipe Lopes", "012/13", "OPALINE-PALLID BLUE",    "237O 144 FNP24 5.0 SOG");
    AddConcurso("Filipe Lopes", "003/03", "OPALINE BLUE",           "237O 019 FNP25 5.0 APF");
    AddConcurso("Filipe Lopes", "003/03", "OPALINE BLUE",           "237O 082 FNP23 5.0 SOG");
    AddConcurso("Filipe Lopes", "014/01", "DOMINANT PIED GREEN",    "237O 084 FNP24 5.0 SOG");
    AddConcurso("Filipe Lopes", "451/02", "A. roseicollis blue (group 3)", "237O 005 FNP25 5.0 APF");
    AddConcurso("Filipe Lopes", "451/02", "A. roseicollis blue (group 3)", "237O 011 FNP24 5.0 SOG");
    AddConcurso("Filipe Lopes", "451/02", "A. roseicollis blue (group 3)", "237O 008 FNP24 5.0 SOG");
    AddConcurso("Filipe Lopes", "451/02", "A. roseicollis blue (group 3)", "237O 003 FNP24 5.0 SOG");
    foreach (var a in new[] {
        "237o/112/24","237o/036/23","237o/010/24","237o/078/24","237o/081/25","237o/062/25",
        "237o/069/25","237o/061/25","237o/073/25","237o/075/25","237o/078/25","237o/053/25",
        "237o/062/23","237o/058/23" })
        AddTransporte("Filipe Lopes", "Danny", a);

    // Armando Alves — 7 concurso
    AddConcurso("Armando Alves", "001/01 A", "A.roseicollis Green",                     "417L 049 FNP24");
    AddConcurso("Armando Alves", "002/02 A", "A.roseicollis Orange face D green",       "417L 017 FNP24");
    AddConcurso("Armando Alves", "002/04 A", "A.roseicollis Opaline green",             "417L 003 FNP24");
    AddConcurso("Armando Alves", "002/04 A", "A.roseicollis Opaline green",             "417L 034 FNP24");
    AddConcurso("Armando Alves", "004/01 A", "A.roseicollis Aqua",                      "417L 009 FNP24");
    AddConcurso("Armando Alves", "004/03 A", "A.roseicollis Opalino aqua",              "417L 025 FNP24");
    AddConcurso("Armando Alves", "010/05 A", "A.roseicollis SL ino aqua",               "417L 033 FNP24");
    AddConcurso("Armando Alves", "012/01 A", "A.roseicollis Pallid D green",            "417L 013 FNP23");

    // Paulo Sousa — 17 concurso + 8 venda + 6 transporte
    AddConcurso("Paulo Sousa", "003/02", "Violet",                                "756M-117-2023");
    AddConcurso("Paulo Sousa", "003/02", "Violet",                                "756M-021-2024");
    AddConcurso("Paulo Sousa", "003/02", "Violet",                                "756M-005-2024");
    AddConcurso("Paulo Sousa", "003/04", "Opaline Violet",                        "756M-110-2023");
    AddConcurso("Paulo Sousa", "003/04", "Opaline Violet",                        "756M-062-2023");
    AddConcurso("Paulo Sousa", "010/04", "SL Ino Blue",                           "756M-084-2023");
    AddConcurso("Paulo Sousa", "012/04", "Palid Blue D blue palid DD",            "756M-016-2024");
    AddConcurso("Paulo Sousa", "012/07", "Palid Violet",                          "756M-008-2024");
    AddConcurso("Paulo Sousa", "012/13", "Opaline palid blue D blue palid DD",    "756M-008-2025");
    AddConcurso("Paulo Sousa", "014/01", "Dominant pied green",                   "756M-091-2023");
    AddConcurso("Paulo Sousa", "014/10", "Opaline dominant green",                "756M-028-2024");
    AddConcurso("Paulo Sousa", "015/04", "Recessive pied blue D recessive blue DD","756M-007-2025");
    AddConcurso("Paulo Sousa", "015/07", "Recessive pied violet",                 "756M-022-2025");
    AddConcurso("Paulo Sousa", "451/02", "A.Roseicollis blue (group 3)",          "756M-029-2024");
    AddConcurso("Paulo Sousa", "451/02", "A.Roseicollis blue (group 3)",          "756M-083-2023");
    AddConcurso("Paulo Sousa", "451/02", "A.Roseicollis blue (group 3)",          "756M-013-2025");
    AddConcurso("Paulo Sousa", "451/02", "A.Roseicollis blue (group 3)",          "756M-014-2025");
    AddVenda("Paulo Sousa", "Blue D",                    "756M-022-2024");
    AddVenda("Paulo Sousa", "Palid Violet",              "756M-108-2023");
    AddVenda("Paulo Sousa", "Opaline blue cinnamon",     "756M-111-2023");
    AddVenda("Paulo Sousa", "Blue palid",                "756M-019-2025");
    AddVenda("Paulo Sousa", "Blue D / palid",            "756M-015-2025");
    AddVenda("Paulo Sousa", "Blue palid",                "756M-012-2025");
    AddVenda("Paulo Sousa", "Opalin palid violet",       "756M-020-2024");
    AddVenda("Paulo Sousa", "Blue D",                    "756M-020-2025");
    foreach (var a in new[] {
        "756M-012-2024","756M-068-2023","756M-027-2024","756M-50-2021",
        "225N-046-2023","756M-044-2025" })
        AddTransporte("Paulo Sousa", "Matteo Soldati", a);

    // Daniel Rodrigues — 3 concurso + 3 venda
    AddConcurso("Daniel Rodrigues", "010/02", "A.roseicollis orange face ino green (orange face lutino)", "156P 19 FNP23");
    AddConcurso("Daniel Rodrigues", "012/01", "A.roseicollis pallid D green",  "156P 25 FNP24");
    AddConcurso("Daniel Rodrigues", "004/01", "A.roseicollis aqua",            "156P 24 FNP24");
    AddVenda("Daniel Rodrigues", "A.roseicollis marbled",                       "087N 50 FNP23");
    AddVenda("Daniel Rodrigues", "A.roseicollis GREEN /INO / ORANGE FACE",      "156P 33 FNP24");
    AddVenda("Daniel Rodrigues", "A.roseicollis",                               "156P 31 FNP24");

    var eBytes = EtiquetasAvery3421PdfGenerator.Render(labels);
    File.WriteAllBytes(outPath, eBytes);
    Console.WriteLine($"OK: {outPath} ({eBytes.Length} bytes, {labels.Count} etiquetas)");
    RenderPng(eBytes, Path.ChangeExtension(outPath, ".png"));
    return;
}

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
