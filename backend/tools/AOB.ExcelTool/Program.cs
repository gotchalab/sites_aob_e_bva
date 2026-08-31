using AOB.Application.Convoyage;
using ClosedXML.Excel;

// Gera um workbook .xlsx de teste directo com dados sintéticos, para verificar
// que as fórmulas ficam bem escritas e recalculam os valores esperados.
//   dotnet run --project backend/tools/AOB.ExcelTool [-o outfile.xlsx]

var outIdx = Array.IndexOf(args, "-o");
var outPath = outIdx >= 0 && outIdx + 1 < args.Length
    ? args[outIdx + 1]
    : "d:/PROJETOS/aob/excel-convoyage-test.xlsx";

// Cargas fictícias (2 cargas).
var transportes = new List<TransportExcelExporter.TransporteRow>
{
    new("Vilela Express",    "T01", 17, "Vilela; Barcelos",  "JOAO (5), MARIA (12)", "Agapornis", 0),
    new("Vilela Express",    "T02",  8, "Barcelos",          "PEDRO (8)",            "Agapornis", 0),
};

// Inscrições fictícias (3 pessoas com diferentes estatutos e mix de aves).
var submittedAt = new DateTime(2026, 8, 10, 14, 30, 0);
var inscricoes = new List<TransportExcelExporter.InscricaoRow>
{
    new(
        SubmissionId: 101, SubmittedAt: submittedAt,
        Nome: "João Sócio",  Email: "joao@test.local",  Telefone: "911111111",
        Pais: "Portugal", LocalRecolha: "Vilela",
        NumAvesConcurso: 5, NumAvesVenda: 0,
        NumAvesTransportePtBe: 0, NumAvesTransporteBePt: 0,
        SocioBva: "Sócio",
        TotalPago: 0m, CargaAtribuida: "T01"),
    new(
        // Maria: 2 aves PT→BE + 2 aves BE→PT. Ocupam 2 espaços (não 4) porque
        // as gaiolas vão cheias PT→BE e voltam cheias BE→PT — MAX(2,2)=2.
        SubmissionId: 102, SubmittedAt: submittedAt,
        Nome: "Maria PagaAgora", Email: "maria@test.local", Telefone: "922222222",
        Pais: "Portugal", LocalRecolha: "Vilela",
        NumAvesConcurso: 8, NumAvesVenda: 4,
        NumAvesTransportePtBe: 2, NumAvesTransporteBePt: 2,
        SocioBva: "Paga na inscrição",
        TotalPago: 0m, CargaAtribuida: "T01"),
    new(
        // Pedro: 3 aves BE→PT, 0 PT→BE. Ocupa 3 espaços.
        SubmissionId: 103, SubmittedAt: submittedAt,
        Nome: "Pedro NãoSócio", Email: "pedro@test.local", Telefone: "933333333",
        Pais: "Portugal", LocalRecolha: "Barcelos",
        NumAvesConcurso: 8, NumAvesVenda: 0,
        NumAvesTransportePtBe: 0, NumAvesTransporteBePt: 3,
        SocioBva: "Não sócio",
        TotalPago: 0m, CargaAtribuida: "T02"),
};

// Aves — replicar as contagens (só as de concurso importam para o COUNTIF por carga
// se usarmos "Carga" == código; aqui contamos concurso+venda+transporte todas para T0X).
var aves = new List<TransportExcelExporter.AveRow>();
void AddAves(int sub, string criador, int n, string carga)
{
    for (int i = 0; i < n; i++)
        aves.Add(new TransportExcelExporter.AveRow(
            SubmissionId: sub, Criador: criador,
            Serie: $"S{i+1:00}", Especie: "Roseicollis",
            Mutacao: "verde", Anilha: $"P-{sub}-{i:00}",
            Equipa: "—", Posicao: "—",
            Tipo: "Concurso",
            CargaAtribuida: carga));
}
AddAves(101, "João Sócio",       5,  "T01");
AddAves(102, "Maria PagaAgora", 12, "T01"); // 8 concurso + 4 venda
AddAves(103, "Pedro NãoSócio",   8,  "T02");

var bytes = TransportExcelExporter.Render(
    year: 2026, tipo: "Agapornis", capacidadePorCarga: 300,
    pricing: TransportExcelExporter.Pricing.Defaults,
    transportes, inscricoes, aves);

File.WriteAllBytes(outPath, bytes);
Console.WriteLine($"✔  Workbook escrito: {outPath} ({bytes.Length:N0} bytes)");

// ── Validação: reabrir o workbook, forçar recálculo e imprimir valores ────────
using var wb = new XLWorkbook(outPath);
wb.RecalculateAllFormulas();

Console.WriteLine();
Console.WriteLine("── Folha 'Preços' (constantes) ─────────────────────────────");
var precos = wb.Worksheet("Preços");
foreach (var row in precos.RangeUsed()!.RowsUsed())
{
    var v = row.Cell(2);
    if (v.Value.IsNumber)
        Console.WriteLine($"  {row.Cell(1).GetString(),-40} = {v.GetDouble(),8:F2}");
}

Console.WriteLine();
Console.WriteLine("── Folha 'Transportes' (Nº aves valor · Sobras fórmula) ────");
var trans = wb.Worksheet("Transportes");
Console.WriteLine($"  {"Transportadora",-14} {"NºAves",8} {"Sobras",8}   fórmula Sobras");
for (int r = 2; r <= 3; r++)
    Console.WriteLine($"  {trans.Cell(r,1).GetString(),-14} {trans.Cell(r,5).GetDouble(),8:F0} " +
                      $"{trans.Cell(r,6).GetDouble(),8:F0}   ={trans.Cell(r,6).FormulaA1}");
Console.WriteLine($"  TOTAL          {trans.Cell(4,5).GetDouble(),8:F0} {trans.Cell(4,6).GetDouble(),8:F0}");

Console.WriteLine();
Console.WriteLine("── Folha 'Inscrições' (custos derivados + BVA split) ───────");
var ins = wb.Worksheet("Inscrições");
// Layout: col 12 = Sócio, col 17 = Transp. adq., col 19 = Total, 20 = Portugal, 21 = Masters.
Console.WriteLine($"  {"Nome",-18} {"Sócio",-20} {"Adq.",8} {"TOTAL",8} {"BVA Port.",10} {"BVA Mstr.",10}");
for (int r = 2; r <= 4; r++)
{
    Console.WriteLine($"  {ins.Cell(r,3).GetString(),-18} {ins.Cell(r,12).GetString(),-20} " +
                      $"{ins.Cell(r,17).GetDouble(),8:F2} " +
                      $"{ins.Cell(r,19).GetDouble(),8:F2} " +
                      $"{ins.Cell(r,20).GetDouble(),10:F2} " +
                      $"{ins.Cell(r,21).GetDouble(),10:F2}");
}
Console.WriteLine($"  {"TOTAL",-18} {"",-20} " +
                  $"{ins.Cell(5,17).GetDouble(),8:F2} " +
                  $"{ins.Cell(5,19).GetDouble(),8:F2} " +
                  $"{ins.Cell(5,20).GetDouble(),10:F2} " +
                  $"{ins.Cell(5,21).GetDouble(),10:F2}");

Console.WriteLine();
Console.WriteLine("── Valores esperados (regra do formulário) ─────────────────");
static void Expected(string nome, int nC, int nV, int nPtBe, int nBePt, string estatuto)
{
    double totalCV = nC + nV;
    double espacos = Math.Max(nPtBe, nBePt); // regra do site: MAX, não soma
    double tarifa = estatuto == "NaoSocio" ? 15.5 : 5.5;
    double tarifaAdq = estatuto == "NaoSocio" ? 20.5 : 15.5;
    double insc = totalCV > 0 ? 8.0 : 0;
    double aves = 3.0 * nC;
    double gai  = 3.0 * totalCV;
    double tr   = tarifa * totalCV;
    double trAdq= tarifaAdq * espacos;
    double quo  = estatuto == "PagaComInscricao" ? 40.0 : 0;
    Console.WriteLine($"  {nome,-18} insc={insc:F2} aves={aves:F2} gai={gai:F2} " +
                      $"tr={tr:F2} adq={trAdq:F2} quo={quo:F2} TOTAL={insc+aves+gai+tr+trAdq+quo:F2}");
}
Expected("João Sócio",      5, 0, 0, 0, "JaSocio");
Expected("Maria PagaAgora", 8, 4, 2, 2, "PagaComInscricao");
Expected("Pedro NãoSócio",  8, 0, 0, 3, "NaoSocio");

Console.WriteLine();
Console.WriteLine("✔  Teste concluído. Abre o ficheiro em Excel para inspecção visual.");
