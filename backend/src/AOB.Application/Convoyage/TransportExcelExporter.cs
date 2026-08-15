using ClosedXML.Excel;

namespace AOB.Application.Convoyage;

/// Gera o ficheiro .xlsx com 3 folhas:
///   1. Transportes — layout do ano anterior (Transportadora | Carga | Nº aves | Zonas | Criadores | Tipo | Sobras).
///   2. Inscrições — 1 linha por inscrição.
///   3. Aves — 1 linha por ave.
public static class TransportExcelExporter
{
    public record TransporteRow(
        string Transportadora, string Codigo, int NumAves,
        string Zonas, string CriadoresLabel, string Tipo, int Sobras);

    public record InscricaoRow(
        int SubmissionId, DateTime SubmittedAt,
        string Nome, string Email, string Telefone, string Pais,
        string LocalRecolha, int NumAvesConcurso, int NumAvesVenda,
        int NumAvesTransporte,
        string SocioBva, decimal TotalPago, string CargaAtribuida);

    public record AveRow(
        int SubmissionId, string Criador, string Serie, string Especie,
        string Mutacao, string Anilha, string Equipa, string Posicao,
        string CargaAtribuida);

    public static byte[] Render(
        int year, string tipo, int capacidadePorCarga,
        List<TransporteRow> transportes,
        List<InscricaoRow> inscricoes,
        List<AveRow> aves)
    {
        using var wb = new XLWorkbook();
        WriteTransportes(wb, tipo, capacidadePorCarga, transportes);
        WriteInscricoes(wb, inscricoes);
        WriteAves(wb, aves);

        wb.Properties.Title = $"Plano de transportes convoyage {year}";
        wb.Properties.Company = "AOB";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteTransportes(
        XLWorkbook wb, string tipo, int cap, List<TransporteRow> rows)
    {
        var ws = wb.Worksheets.Add("Transportes");

        var headers = new[] { "Carga", "Nº aves", "Zonas", "Criadores", "Tipo", "Sobras" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromArgb(26, 67, 128);
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.Codigo;
            ws.Cell(r, 2).Value = row.NumAves;
            ws.Cell(r, 3).Value = row.Zonas;
            ws.Cell(r, 4).Value = row.CriadoresLabel;
            ws.Cell(r, 5).Value = string.IsNullOrWhiteSpace(row.Tipo) ? tipo : row.Tipo;
            ws.Cell(r, 6).Value = row.Sobras;

            // Destaques visuais.
            if (row.NumAves > cap)
            {
                ws.Cell(r, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(255, 220, 220);
                ws.Cell(r, 2).Style.Font.Bold = true;
            }
            if (row.Sobras > 4)
            {
                ws.Cell(r, 6).Style.Fill.BackgroundColor = XLColor.FromArgb(255, 240, 200);
            }

            r++;
        }

        var data = ws.Range(2, 1, Math.Max(2, r - 1), headers.Length);
        data.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        data.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        ws.Column(4).Style.Alignment.WrapText = true;

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 8;
        ws.Column(3).Width = 22;
        ws.Column(4).Width = 70;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 8;

        ws.SheetView.FreezeRows(1);

        // Totais.
        if (rows.Count > 0)
        {
            var totalRow = r;
            ws.Cell(totalRow, 1).Value = "TOTAL";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            ws.Cell(totalRow, 2).FormulaA1 = $"SUM(B2:B{totalRow - 1})";
            ws.Cell(totalRow, 2).Style.Font.Bold = true;
            ws.Cell(totalRow, 6).FormulaA1 = $"SUM(F2:F{totalRow - 1})";
            ws.Cell(totalRow, 6).Style.Font.Bold = true;
        }
    }

    private static void WriteInscricoes(XLWorkbook wb, List<InscricaoRow> rows)
    {
        var ws = wb.Worksheets.Add("Inscrições");

        var headers = new[]
        {
            "#", "Submetido em", "Nome", "Email", "Telefone", "País",
            "Local de recolha", "Aves concurso", "Aves venda", "Aves transporte", "Sócio BVA",
            "Total pago (€)", "Carga",
        };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromArgb(26, 67, 128);
        header.Style.Font.FontColor = XLColor.White;

        int r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.SubmissionId;
            ws.Cell(r, 2).Value = row.SubmittedAt;
            ws.Cell(r, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            ws.Cell(r, 3).Value = row.Nome;
            ws.Cell(r, 4).Value = row.Email;
            ws.Cell(r, 5).Value = row.Telefone;
            ws.Cell(r, 6).Value = row.Pais;
            ws.Cell(r, 7).Value = row.LocalRecolha;
            ws.Cell(r, 8).Value = row.NumAvesConcurso;
            ws.Cell(r, 9).Value = row.NumAvesVenda;
            ws.Cell(r, 10).Value = row.NumAvesTransporte;
            ws.Cell(r, 11).Value = row.SocioBva;
            ws.Cell(r, 12).Value = row.TotalPago;
            ws.Cell(r, 12).Style.NumberFormat.Format = "0.00";
            ws.Cell(r, 13).Value = row.CargaAtribuida;
            r++;
        }

        ws.Columns(1, headers.Length).AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void WriteAves(XLWorkbook wb, List<AveRow> rows)
    {
        var ws = wb.Worksheets.Add("Aves");

        var headers = new[]
        {
            "Inscrição #", "Criador", "Série", "Espécie", "Mutação",
            "Anilha", "Equipa (T)", "Pos.", "Carga",
        };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromArgb(26, 67, 128);
        header.Style.Font.FontColor = XLColor.White;

        int r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.SubmissionId;
            ws.Cell(r, 2).Value = row.Criador;
            ws.Cell(r, 3).Value = row.Serie;
            ws.Cell(r, 4).Value = EspecieLabel(row.Especie);
            ws.Cell(r, 5).Value = row.Mutacao;
            ws.Cell(r, 6).Value = row.Anilha;
            ws.Cell(r, 7).Value = row.Equipa;
            ws.Cell(r, 8).Value = row.Posicao;
            ws.Cell(r, 9).Value = row.CargaAtribuida;
            r++;
        }

        ws.Columns(1, headers.Length).AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    // Mapeia o valor do enum de espécie (guardado em cru no JSON da submissão)
    // para o rótulo científico usado no formulário: "Nigrigenis" → "A. nigrigenis".
    private static string EspecieLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var v = raw.Trim();
        return v switch
        {
            "Roseicollis" => "A. roseicollis",
            "Personatus"  => "A. personatus",
            "Fischeri"    => "A. fischeri",
            "Nigrigenis"  => "A. nigrigenis",
            "Lilianae"    => "A. lilianae",
            "Canus"       => "A. canus",
            "Taranta"     => "A. taranta",
            "Pullarius"   => "A. pullarius",
            _ => v,
        };
    }
}
