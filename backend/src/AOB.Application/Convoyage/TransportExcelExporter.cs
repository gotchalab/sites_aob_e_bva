using AOB.Core.Entities;
using ClosedXML.Excel;

namespace AOB.Application.Convoyage;

/// Gera o ficheiro .xlsx com 4 folhas:
///   1. Preços — constantes editáveis (tarifas, quotas, capacidade) referenciadas pelas restantes folhas.
///   2. Transportes — Nº aves e Sobras derivados por fórmula sobre a folha Aves.
///   3. Inscrições — colunas de custo derivadas por fórmula a partir de Preços; Total = SUM.
///   4. Aves — 1 linha por ave (fonte de verdade para as contagens/atribuições).
public static class TransportExcelExporter
{
    public record TransporteRow(
        string Transportadora, string Codigo, int NumAves,
        string Zonas, string CriadoresLabel, string Tipo, int Sobras,
        // Aves que fisicamente ocupam o camião em cada sentido — as trPtBe
        // (Vende) ficam na Bélgica, as trBePt (Compra) só ocupam na volta.
        // Serve para calcular sobras por sentido de forma consistente.
        int NumAvesIda,
        int NumAvesRegresso);

    /// <summary>
    /// SocioBva deve ser um de: "Sócio", "Paga na inscrição", "Não sócio".
    /// Estes valores são referenciados directamente pelas fórmulas de tarifa/quota.
    /// </summary>
    /// <remarks>
    /// NumAvesTransportePtBe / NumAvesTransporteBePt: contagens por sentido. Cada
    /// gaiola no camião conta como UM espaço mesmo que vá cheia num sentido e
    /// volte cheia no outro — por isso o custo do transporte adquirente é sobre
    /// MAX(PT→BE, BE→PT), não a soma.
    ///
    /// NumAvesVendaOferecidas: aves de venda que o criador recebeu de oferta —
    /// contam nas contagens (ocupam gaiola física) mas descontam de gaiolas e
    /// transporte de exposição na factura.
    /// NumAvesEspacosOferecidos: nº de *espaços* de transporte adquirido
    /// oferecidos (não é nº de aves — é o desconto directo à coluna Transp. adq.).
    /// </remarks>
    public record InscricaoRow(
        int SubmissionId, DateTime SubmittedAt,
        string Nome, string Email, string Telefone, string Pais,
        string LocalRecolha, int NumAvesConcurso, int NumAvesVenda,
        int NumAvesTransportePtBe, int NumAvesTransporteBePt,
        int NumAvesVendaOferecidas, int NumAvesEspacosOferecidos,
        string SocioBva, decimal TotalPago, string CargaAtribuida);

    // Valores permitidos na coluna Sócio (usados na data validation e nas fórmulas).
    public const string SOCIO_SIM        = "Sócio";
    public const string SOCIO_PAGA_INSCR = "Paga na inscrição";
    public const string SOCIO_NAO        = "Não sócio";

    /// <summary>Tarifas e taxas do ano — usadas como defaults na folha "Preços".</summary>
    public record Pricing(
        decimal Inscricao, decimal AveBva, decimal Gaiola,
        decimal TarifaTranspSocio, decimal TarifaTranspNaoSocio,
        decimal TarifaAdqSocio, decimal TarifaAdqNaoSocio,
        decimal Quota)
    {
        public static Pricing Defaults => new(8.00m, 3.00m, 3.00m, 5.50m, 15.50m, 15.50m, 20.50m, 40.00m);
    }

    public record AveRow(
        int SubmissionId, string Criador, string Serie, string Especie,
        string Mutacao, string Anilha, string Equipa, string Posicao,
        string Tipo, string CargaAtribuida);

    // ── Layout da folha "Preços" ─────────────────────────────────────────────
    // Coluna B contém os valores editáveis. Named ranges facilitam as fórmulas.
    private const string PRICES_SHEET = "Preços";
    private const string R_CAP        = "CapacidadePorCarga";
    private const string R_INSCR      = "PrecoInscricao";
    private const string R_AVES       = "PrecoAveBva";
    private const string R_GAIOLAS    = "PrecoGaiolaBva";
    private const string R_TAR_S      = "TarifaTransporteSocio";
    private const string R_TAR_NS     = "TarifaTransporteNaoSocio";
    private const string R_ADQ_S      = "TarifaAdquirenteSocio";
    private const string R_ADQ_NS     = "TarifaAdquirenteNaoSocio";
    private const string R_QUOTA      = "Quota";

    public static byte[] Render(
        int year, string tipo, int capacidadePorCarga,
        Pricing pricing,
        List<TransporteRow> transportes,
        List<InscricaoRow> inscricoes,
        List<AveRow> aves)
    {
        using var wb = new XLWorkbook();
        WritePrecos(wb, capacidadePorCarga, pricing);
        WriteTransportes(wb, tipo, transportes);
        WriteInscricoes(wb, inscricoes);
        WriteAves(wb, aves);

        wb.Properties.Title = $"Plano de transportes convoyage {year}";
        wb.Properties.Company = "AOB";

        // Sem isto, ClosedXML escreve as fórmulas mas sem valor cache; algumas versões
        // do Excel abrem o ficheiro (sobretudo em "Vista Protegida") e mostram as
        // células vazias até o utilizador activar edição e premir F9.
        //   1) RecalculateAllFormulas preenche a maioria da cache
        //   2) FullCalculationOnLoad força o Excel a recalcular ao abrir (para os
        //      casos em que ClosedXML não consegue avaliar — ex.: nomes definidos
        //      dentro de IF).
        wb.CalculateMode = XLCalculateMode.Auto;
        wb.RecalculateAllFormulas();
        wb.FullCalculationOnLoad = true;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WritePrecos(XLWorkbook wb, int capacidadePorCarga, Pricing p)
    {
        var ws = wb.Worksheets.Add(PRICES_SHEET);

        void SectionHeader(int row, string title)
        {
            ws.Range(row, 1, row, 3).Merge();
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(26, 67, 128);
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
        }

        // ── Secção 1: Configuração vinda do backoffice (só leitura) ──────────
        SectionHeader(1, "Configuração (definida no backoffice — só leitura)");

        ws.Cell(2, 1).Value = "Capacidade máxima por transportadora";
        var capCell = ws.Cell(2, 2);
        capCell.Value = capacidadePorCarga;
        capCell.Style.NumberFormat.Format = "0";
        capCell.Style.Fill.BackgroundColor = XLColor.FromArgb(232, 232, 232);
        capCell.Style.Font.Italic = true;
        capCell.Style.Protection.Locked = true;
        wb.NamedRanges.Add(R_CAP,
            $"'{PRICES_SHEET}'!${capCell.Address.ColumnLetter}${capCell.Address.RowNumber}");
        ws.Cell(2, 3).Value = "Alterar no backoffice → Configuração do ano";
        ws.Cell(2, 3).Style.Font.Italic = true;
        ws.Cell(2, 3).Style.Font.FontColor = XLColor.FromArgb(120, 120, 120);

        // Proteger a folha permite manter o resto editável mas travar a capacidade.
        ws.Protect().AllowElement(XLSheetProtectionElements.FormatCells)
                    .AllowElement(XLSheetProtectionElements.FormatColumns)
                    .AllowElement(XLSheetProtectionElements.FormatRows)
                    .AllowElement(XLSheetProtectionElements.SelectLockedCells)
                    .AllowElement(XLSheetProtectionElements.SelectUnlockedCells);

        // ── Secção 2: Tarifas e taxas editáveis ──────────────────────────────
        SectionHeader(4, "Tarifas e taxas (editáveis — recalculam as inscrições)");

        // Cabeçalhos das colunas da tabela de preços.
        ws.Cell(5, 1).Value = "Parâmetro";
        ws.Cell(5, 2).Value = "Valor (€)";
        ws.Cell(5, 3).Value = "Notas";
        var subHeader = ws.Range(5, 1, 5, 3);
        subHeader.Style.Font.Bold = true;
        subHeader.Style.Fill.BackgroundColor = XLColor.FromArgb(230, 235, 245);

        var linhas = new (string Rot, double Val, string Named, string Nota)[]
        {
            ("Inscrição (por inscrição)",      (double)p.Inscricao,            R_INSCR,   "Aplicada se houver aves concurso + venda"),
            ("Aves BVA (por ave concurso)",    (double)p.AveBva,               R_AVES,    "Multiplica pelo nº aves concurso"),
            ("Gaiolas (por ave concurso+venda)", (double)p.Gaiola,             R_GAIOLAS, "Multiplica por (concurso + venda)"),
            ("Tarifa transporte sócio",        (double)p.TarifaTranspSocio,    R_TAR_S,   "Para concurso+venda de sócios"),
            ("Tarifa transporte não sócio",    (double)p.TarifaTranspNaoSocio, R_TAR_NS,  "Para concurso+venda de não sócios"),
            ("Tarifa adquirente sócio",        (double)p.TarifaAdqSocio,       R_ADQ_S,   "Aves de transporte, sócio"),
            ("Tarifa adquirente não sócio",    (double)p.TarifaAdqNaoSocio,    R_ADQ_NS,  "Aves de transporte, não sócio"),
            ("Quota",                          (double)p.Quota,                R_QUOTA,   "Aplicada se paga com inscrição"),
        };

        int r = 6;
        foreach (var (rot, val, named, nota) in linhas)
        {
            ws.Cell(r, 1).Value = rot;
            var cell = ws.Cell(r, 2);
            cell.Value = val;
            cell.Style.NumberFormat.Format = "0.00";
            cell.Style.Protection.Locked = false; // desbloqueada para edição
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(255, 249, 220);
            wb.NamedRanges.Add(named,
                $"'{PRICES_SHEET}'!${cell.Address.ColumnLetter}${cell.Address.RowNumber}");
            ws.Cell(r, 3).Value = nota;
            r++;
        }

        ws.Column(1).Width = 40;
        ws.Column(2).Width = 12;
        ws.Column(3).Width = 44;

        var data = ws.Range(5, 1, r - 1, 3);
        data.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        data.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
    }

    private static void WriteTransportes(
        XLWorkbook wb, string tipo, List<TransporteRow> rows)
    {
        var ws = wb.Worksheets.Add("Transportes");

        //  A Transportadora  B Zonas  C Criadores  D Tipo
        //  E Nº aves (gaiolas efectivas, max por criador entre PtBe/BePt)
        //  F Sobras (contra E — critério de capacidade da carga)
        //  G Nº aves ida       H Sobras ida
        //  I Nº aves regresso  J Sobras regresso
        var headers = new[]
        {
            "Transportadora", "Zonas", "Criadores", "Tipo",
            "Nº aves", "Sobras",
            "Nº aves ida", "Sobras ida",
            "Nº aves regresso", "Sobras regresso",
        };
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
            ws.Cell(r, 2).Value = row.Zonas;
            ws.Cell(r, 3).Value = row.CriadoresLabel;
            ws.Cell(r, 4).Value = string.IsNullOrWhiteSpace(row.Tipo) ? tipo : row.Tipo;
            // Nº aves (gaiolas efectivas) — é o que compara com a capacidade.
            ws.Cell(r, 5).Value = row.NumAves;
            ws.Cell(r, 6).FormulaA1 = $"MAX(0, {R_CAP} - $E{r})";
            // Aves na ida: concurso + venda + transporte-Vende (PT→BE).
            ws.Cell(r, 7).Value = row.NumAvesIda;
            ws.Cell(r, 8).FormulaA1 = $"MAX(0, {R_CAP} - $G{r})";
            // Aves na volta: concurso + venda + transporte-Compra (BE→PT).
            ws.Cell(r, 9).Value = row.NumAvesRegresso;
            ws.Cell(r, 10).FormulaA1 = $"MAX(0, {R_CAP} - $I{r})";
            r++;
        }

        // Formatação condicional:
        //   - Nº aves acima da capacidade → vermelho + bold (overflow)
        //   - Sobras > 0 → amarelo (qualquer buraco livre é sinalizado)
        if (rows.Count > 0)
        {
            var lastData = r - 1;
            var amarelo = XLColor.FromArgb(255, 240, 200);
            var vermelho = XLColor.FromArgb(255, 220, 220);

            foreach (var col in new[] { 5, 7, 9 })
            {
                ws.Range(2, col, lastData, col).AddConditionalFormat()
                    .WhenGreaterThan(R_CAP)
                    .Fill.SetBackgroundColor(vermelho)
                    .Font.SetBold();
            }
            foreach (var col in new[] { 6, 8, 10 })
            {
                ws.Range(2, col, lastData, col).AddConditionalFormat()
                    .WhenGreaterThan(0)
                    .Fill.SetBackgroundColor(amarelo);
            }
        }

        var data = ws.Range(2, 1, Math.Max(2, r - 1), headers.Length);
        data.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        data.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        ws.Column(3).Style.Alignment.WrapText = true;

        ws.Column(1).Width = 14; // Transportadora
        ws.Column(2).Width = 22; // Zonas
        ws.Column(3).Width = 70; // Criadores
        ws.Column(4).Width = 14; // Tipo
        ws.Column(5).Width = 10; // Nº aves
        ws.Column(6).Width = 10; // Sobras
        ws.Column(7).Width = 12; // Nº aves ida
        ws.Column(8).Width = 12; // Sobras ida
        ws.Column(9).Width = 14; // Nº aves regresso
        ws.Column(10).Width = 14; // Sobras regresso

        ws.SheetView.FreezeRows(1);

        if (rows.Count > 0)
        {
            var totalRow = r;
            ws.Cell(totalRow, 1).Value = "TOTAL";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            foreach (var col in new[] { 5, 6, 7, 8, 9, 10 })
            {
                var letter = XLHelper.GetColumnLetterFromNumber(col);
                ws.Cell(totalRow, col).FormulaA1 = $"SUM({letter}2:{letter}{totalRow - 1})";
                ws.Cell(totalRow, col).Style.Font.Bold = true;
            }
        }
    }

    private static void WriteInscricoes(XLWorkbook wb, List<InscricaoRow> rows)
    {
        var ws = wb.Worksheets.Add("Inscrições");

        // Layout:
        //  A #  B Submetido em  C Nome  D Email  E Telefone  F País  G Local recolha
        //  H Aves concurso  I Aves venda
        //  J Aves T. PT→BE (aves que partem de PT — "Vende")
        //  K Aves T. BE→PT (aves que chegam a PT — "Compra")
        //  L Venda oferec.  M Espaços oferec. (descontos que não alteram as contagens H..K)
        //  N Sócio
        //  O Inscrição  P Aves BVA  Q Gaiolas  R Transporte  S Transp. adq.
        //  T Quota  U Total (€)  V BVA Portugal (€)  W BVA Masters (€)  X Transportadora
        var headers = new[]
        {
            "#", "Submetido em", "Nome", "Email", "Telefone", "País",
            "Local de recolha", "Aves concurso", "Aves venda",
            "Aves T. PT→BE", "Aves T. BE→PT",
            "Venda oferec.", "Espaços oferec.",
            "Sócio",
            "Inscrição (€)", "Aves BVA (€)", "Gaiolas (€)", "Transporte (€)",
            "Transp. adq. (€)", "Quota (€)",
            "Total pago (€)", "BVA Portugal (€)", "BVA Masters (€)",
            "Transportadora",
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
            ws.Cell(r, 10).Value = row.NumAvesTransportePtBe;
            ws.Cell(r, 11).Value = row.NumAvesTransporteBePt;
            ws.Cell(r, 12).Value = row.NumAvesVendaOferecidas;
            ws.Cell(r, 13).Value = row.NumAvesEspacosOferecidos;
            ws.Cell(r, 14).Value = row.SocioBva;

            // Fórmulas de custo — a coluna Sócio (N) é o único discriminador.
            // As ofertas descontam do faturável mas as colunas H..K ficam com as
            // contagens reais (aves físicas / espaços físicos no camião).
            ws.Cell(r, 15).FormulaA1 = $"IF(($H{r}+$I{r})>0,{R_INSCR},0)";
            ws.Cell(r, 16).FormulaA1 = $"{R_AVES}*$H{r}";
            ws.Cell(r, 17).FormulaA1 = $"{R_GAIOLAS}*MAX(0,$H{r}+$I{r}-$L{r})";
            ws.Cell(r, 18).FormulaA1 =
                $"IF($N{r}=\"{SOCIO_NAO}\",{R_TAR_NS},{R_TAR_S})*MAX(0,$H{r}+$I{r}-$L{r})";
            // Transp. adq. = tarifa × MAX(0, MAX(PT→BE, BE→PT) − espaços oferecidos).
            // Cada gaiola do camião conta uma vez mesmo que vá cheia num sentido e
            // volte cheia no outro.
            ws.Cell(r, 19).FormulaA1 =
                $"IF($N{r}=\"{SOCIO_NAO}\",{R_ADQ_NS},{R_ADQ_S})*MAX(0,MAX($J{r},$K{r})-$M{r})";
            ws.Cell(r, 20).FormulaA1 = $"IF($N{r}=\"{SOCIO_PAGA_INSCR}\",{R_QUOTA},0)";
            ws.Cell(r, 21).FormulaA1 = $"SUM($O{r}:$T{r})";
            // BVA Masters = Inscrição (O) + Aves BVA (P).
            // Aves BVA já multiplica só pelas aves concurso; aves venda pagam só a gaiola.
            ws.Cell(r, 23).FormulaA1 = $"$O{r}+$P{r}";
            // BVA Portugal: o que sobra do total pago depois de pagar à Masters.
            ws.Cell(r, 22).FormulaA1 = $"$U{r}-$W{r}";

            for (int c = 15; c <= 23; c++)
                ws.Cell(r, c).Style.NumberFormat.Format = "0.00";

            ws.Cell(r, 24).Value = row.CargaAtribuida;
            r++;
        }

        // Data validation na coluna Sócio: dropdown com os 3 valores válidos.
        if (rows.Count > 0)
        {
            var socioRange = ws.Range(2, 14, r - 1, 14);
            var dv = socioRange.CreateDataValidation();
            dv.List($"\"{SOCIO_SIM},{SOCIO_PAGA_INSCR},{SOCIO_NAO}\"", true);
            dv.InCellDropdown = true;
            dv.ErrorStyle = XLErrorStyle.Warning;
            dv.ErrorTitle = "Valor inválido";
            dv.ErrorMessage = $"Usa um de: {SOCIO_SIM}, {SOCIO_PAGA_INSCR}, {SOCIO_NAO}";
        }

        // Zebra striping: cinza-claro alternado para facilitar leitura horizontal.
        // Cada linha ímpar de dados (r=3,5,7,...) ganha fundo. As colunas de
        // síntese (21..23) recebem depois cor própria e substituem o zebra.
        if (rows.Count > 0)
        {
            for (int rr = 3; rr < r; rr += 2)
                ws.Range(rr, 1, rr, headers.Length).Style.Fill.BackgroundColor =
                    XLColor.FromArgb(245, 246, 248);
        }

        // Destaque das colunas de síntese: Total (neutro), BVA Portugal (verde), BVA Masters (laranja).
        // Aplicado depois do zebra: as 3 colunas passam a ter cor plana em vez de
        // stripes — reforça a leitura vertical dos totais.
        if (rows.Count > 0)
        {
            var lastData = r - 1;
            // Header + dados — cada uma com cor distinta.
            ws.Cell(1, 21).Style.Fill.BackgroundColor = XLColor.FromArgb(80, 96, 128);   // Total (azul-escuro)
            ws.Cell(1, 22).Style.Fill.BackgroundColor = XLColor.FromArgb(56, 118, 74);   // BVA Portugal (verde)
            ws.Cell(1, 23).Style.Fill.BackgroundColor = XLColor.FromArgb(180, 95, 6);    // BVA Masters (laranja)

            ws.Range(2, 21, lastData, 21).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 234, 244); // Total
            ws.Range(2, 22, lastData, 22).Style.Fill.BackgroundColor = XLColor.FromArgb(217, 234, 211); // Portugal
            ws.Range(2, 23, lastData, 23).Style.Fill.BackgroundColor = XLColor.FromArgb(252, 229, 205); // Masters
        }

        // Linha TOTAL
        if (rows.Count > 0)
        {
            var totalRow = r;
            ws.Cell(totalRow, 3).Value = "TOTAL";
            ws.Cell(totalRow, 3).Style.Font.Bold = true;
            for (int col = 8; col <= 13; col++) // Aves concurso/venda/tr PT→BE/BE→PT + oferecidas
            {
                ws.Cell(totalRow, col).FormulaA1 =
                    $"SUM({ws.Cell(2, col).Address.ColumnLetter}2:{ws.Cell(2, col).Address.ColumnLetter}{totalRow - 1})";
                ws.Cell(totalRow, col).Style.Font.Bold = true;
            }
            for (int col = 15; col <= 23; col++)
            {
                ws.Cell(totalRow, col).FormulaA1 =
                    $"SUM({ws.Cell(2, col).Address.ColumnLetter}2:{ws.Cell(2, col).Address.ColumnLetter}{totalRow - 1})";
                ws.Cell(totalRow, col).Style.NumberFormat.Format = "0.00";
                ws.Cell(totalRow, col).Style.Font.Bold = true;
            }
            ws.Range(totalRow, 1, totalRow, headers.Length).Style.Fill.BackgroundColor =
                XLColor.FromArgb(240, 240, 240);
            // Preservar destaque das 3 colunas de síntese na linha TOTAL (tons mais saturados).
            ws.Cell(totalRow, 21).Style.Fill.BackgroundColor = XLColor.FromArgb(197, 208, 232);
            ws.Cell(totalRow, 22).Style.Fill.BackgroundColor = XLColor.FromArgb(182, 215, 168);
            ws.Cell(totalRow, 23).Style.Fill.BackgroundColor = XLColor.FromArgb(249, 203, 156);
        }

        ws.Columns(1, headers.Length).AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void WriteAves(XLWorkbook wb, List<AveRow> rows)
    {
        var ws = wb.Worksheets.Add("Aves");

        var headers = new[] { "Inscrição #", "Criador", "Espécie", "Anilha", "Tipo", "Transportadora" };
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
            ws.Cell(r, 3).Value = EspecieLabel(row.Especie);
            ws.Cell(r, 4).Value = row.Anilha;
            ws.Cell(r, 5).Value = row.Tipo;
            ws.Cell(r, 6).Value = row.CargaAtribuida;
            r++;
        }

        ws.Columns(1, headers.Length).AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    // Mapeia o valor do enum de espécie (guardado em cru no JSON da submissão)
    // para o rótulo científico usado no formulário: "Nigrigenis" → "A. nigrigenis".
    private static string EspecieLabel(string? raw)
    {
        var v = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(v)) return "";
        return Enum.TryParse<SpeciesCode>(v, ignoreCase: true, out var s)
            ? SpeciesGenus.Short(s)
            : v;
    }
}
