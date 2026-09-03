using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Snippets.Font;

namespace AOB.Application.Convoyage;

// Gera um PDF de etiquetas Avery 3421 (Zweckform 3421):
//   A4, 3 colunas × 11 linhas = 33 etiquetas por folha, 70mm × 25.4mm cada,
//   margem superior 8.8mm, sem margens laterais, sem gaps.
//
// Layout declarativo em `AveryLabelSheet` (à la pylabels): sheet/label
// dims, margens, gaps e padding interno todos explícitos e validados
// (Validate() atira se a soma exceder a folha).
//
// Fonte: sempre Liberation Sans embebida (via ArialFontResolver) — garante
// que dev (Windows) e prod (Linux/VPS) produzem exactamente o mesmo output,
// independente das fontes instaladas no sistema. Sem esta embed, prod caía
// para DejaVu (métricas incompatíveis) e disparava shrink-to-fit em cascata.
//
// Baselines das 4 linhas: valores calibrados do sample de referência,
// medidos com pdfplumber e mantidos como constantes (BaselinesMm) — preserva
// o alinhamento sub-milimétrico já validado contra a folha física.
//
// Debug: definir env var PDF_DEBUG_GRID=1 desenha o bordo de cada célula
// (cinza) e a área interior de texto (rosa) para inspecção visual.
public static class EtiquetasAvery3421PdfGenerator
{
    static EtiquetasAvery3421PdfGenerator()
    {
        var existing = GlobalFontSettings.FontResolver;
        if (existing is not ArialFontResolver and not ArialWithFallbackResolver)
        {
            GlobalFontSettings.FontResolver = existing is null
                ? (IFontResolver)new ArialFontResolver()
                : new ArialWithFallbackResolver(new ArialFontResolver(), existing);
        }
    }

    private sealed class ArialWithFallbackResolver : IFontResolver
    {
        private readonly ArialFontResolver _primary;
        private readonly IFontResolver _fallback;
        public ArialWithFallbackResolver(ArialFontResolver primary, IFontResolver fallback)
        { _primary = primary; _fallback = fallback; }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
            => _primary.ResolveTypeface(familyName, isBold, isItalic)
               ?? _fallback.ResolveTypeface(familyName, isBold, isItalic);

        public byte[]? GetFont(string faceName)
            => _primary.GetFont(faceName) ?? _fallback.GetFont(faceName);
    }

    public enum EtiquetaTipo { Concurso, Venda, Transporte }

    public record EtiquetaLabel(
        string Nome,
        string Anilha,
        string LinhaDescricao,
        string LinhaTipoOuSerie,
        EtiquetaTipo Tipo);

    public record EtiquetaGrupo(string Header, IReadOnlyList<EtiquetaLabel> Labels);

    // ── Modelo de folha ─────────────────────────────────────────────────────
    private const double MmToPt = 72.0 / 25.4;

    // AveryLabelSheet descreve completamente o layout físico da folha, com
    // todas as dimensões em mm. `Validate()` verifica que
    //   cols*labelW + (cols-1)*colGap + leftMargin + rightMargin == sheetW
    //   rows*labelH + (rows-1)*rowGap + topMargin + bottomMargin == sheetH
    // Se alguma diferença for maior que 0.05mm, atira — apanha erros de setup.
    private sealed record AveryLabelSheet(
        double SheetW, double SheetH,
        int Cols, int Rows,
        double LabelW, double LabelH,
        double LeftMargin, double TopMargin,
        double ColGap, double RowGap,
        double PadLeft, double PadRight,
        double PadTop, double PadBottom)
    {
        public double RightMargin => SheetW - LeftMargin - Cols * LabelW - (Cols - 1) * ColGap;
        public double BottomMargin => SheetH - TopMargin - Rows * LabelH - (Rows - 1) * RowGap;
        public int Perpage => Cols * Rows;

        public double CellXPt(int col) => (LeftMargin + col * (LabelW + ColGap)) * MmToPt;
        public double CellYPt(int row) => (TopMargin + row * (LabelH + RowGap)) * MmToPt;
        public double CellWpt => LabelW * MmToPt;
        public double CellHpt => LabelH * MmToPt;

        public double InnerXPt(int col) => CellXPt(col) + PadLeft * MmToPt;
        public double InnerYPt(int row) => CellYPt(row) + PadTop * MmToPt;
        public double InnerWpt => (LabelW - PadLeft - PadRight) * MmToPt;
        public double InnerHpt => (LabelH - PadTop - PadBottom) * MmToPt;

        public void Validate()
        {
            const double eps = 0.05;
            var rm = RightMargin; var bm = BottomMargin;
            if (rm < -eps || bm < -eps)
                throw new InvalidOperationException(
                    $"Sheet layout excede a folha: rightMargin={rm:F3}mm bottomMargin={bm:F3}mm");
            if (LabelW <= PadLeft + PadRight || LabelH <= PadTop + PadBottom)
                throw new InvalidOperationException("Padding maior que o tamanho da etiqueta");
        }
    }

    // Avery 3421 — 33 etiquetas por A4, sem gap, sem margem lateral,
    // 8.8mm de margem topo e fundo. Padding horizontal 1.5mm cada lado; o
    // padding vertical é zero porque as baselines das 4 linhas são fixas
    // (calibradas pelo sample) em vez de distribuídas uniformemente.
    private static readonly AveryLabelSheet Avery3421 = new(
        SheetW: 210.0, SheetH: 297.0,
        Cols: 3, Rows: 11,
        LabelW: 70.0, LabelH: 25.4,
        LeftMargin: 0.0, TopMargin: 8.8,
        ColGap: 0.0, RowGap: 0.0,
        PadLeft: 1.5, PadRight: 1.5,
        PadTop: 0.0, PadBottom: 0.0);

    // Baselines das 4 linhas, em mm do topo da célula. Valores extraídos do
    // sample fornecido pelo utilizador
    // (_local/examples/etiquetas_PACOS_DE_FERREIRA_avery3421_25.4mm.pdf) e
    // confirmam alinhamento sub-milimétrico com a folha física já em uso.
    // NÃO alterar sem re-medir o sample — mudar afecta directamente a
    // posição vertical dos textos na etiqueta impressa.
    private static readonly double[] BaselinesMm = { 8.61, 11.79, 15.31, 18.84 };

    private const string FontFamily = "Arial";

    // Font sizes extraídos do sample ReportLab (_local/examples/…3421_25.4mm.pdf)
    // e mantidos idênticos para preservar o look visual já validado.
    private const double SizeAnilha = 7.2;
    private const double SizeDesc   = 7.5;
    private const double SizeTipo   = 8.5;
    private const double SizeNome   = 8.5;
    private const double SizeHeader = 9.0;

    public static byte[] Render(IEnumerable<EtiquetaLabel> labels)
        => Render(new[] { new EtiquetaGrupo("", (labels ?? Array.Empty<EtiquetaLabel>()).ToList()) });

    public static byte[] Render(IEnumerable<EtiquetaGrupo> groups)
        => Render(groups, separateSheetsPerGroup: true);

    public static byte[] Render(IEnumerable<EtiquetaGrupo> groups, bool separateSheetsPerGroup)
    {
        Avery3421.Validate();

        var grupos = (groups ?? Array.Empty<EtiquetaGrupo>())
            .Where(g => g.Labels is { Count: > 0 })
            .ToList();

        var doc = new PdfDocument();
        doc.Info.Title = "Etiquetas Convoyage";

        var fontAnilha = new XFont(FontFamily, SizeAnilha, XFontStyleEx.Regular);
        var fontDesc   = new XFont(FontFamily, SizeDesc,   XFontStyleEx.Regular);
        var fontTipo   = new XFont(FontFamily, SizeTipo,   XFontStyleEx.Bold);
        var fontNome   = new XFont(FontFamily, SizeNome,   XFontStyleEx.Bold);
        var fontHeader = new XFont(FontFamily, SizeHeader, XFontStyleEx.Bold);
        var red        = new XSolidBrush(XColor.FromArgb(200, 30, 30));
        var black      = XBrushes.Black;

        var lines = new[] {
            new LineSpec(fontAnilha, black, l => l.Anilha),
            new LineSpec(fontDesc,   black, l => l.LinhaDescricao),
            new LineSpec(fontTipo,   red,   l => l.LinhaTipoOuSerie),
            new LineSpec(fontNome,   black, l => l.Nome.ToUpperInvariant()),
        };

        int perPage = Avery3421.Perpage;

        void DrawOne(XGraphics g, EtiquetaLabel lab, int posInPage)
        {
            int row = posInPage / Avery3421.Cols;
            int col = posInPage % Avery3421.Cols;
            DrawLabel(g, lab, row, col, lines);
        }

        if (separateSheetsPerGroup)
        {
            foreach (var grupo in grupos)
            {
                PdfPage? page = null;
                XGraphics? g = null;
                try
                {
                    for (int i = 0; i < grupo.Labels.Count; i++)
                    {
                        int posInPage = i % perPage;
                        if (posInPage == 0)
                        {
                            g?.Dispose();
                            page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.A4;
                            g = XGraphics.FromPdfPage(page);
                            DrawGroupHeader(g, grupo.Header, fontHeader, black);
                            MaybeDrawDebugGrid(g);
                        }
                        DrawOne(g!, grupo.Labels[i], posInPage);
                    }
                }
                finally { g?.Dispose(); }
            }
        }
        else
        {
            var flat = grupos.SelectMany(g => g.Labels).ToList();
            PdfPage? page = null;
            XGraphics? g0 = null;
            try
            {
                for (int i = 0; i < flat.Count; i++)
                {
                    int posInPage = i % perPage;
                    if (posInPage == 0)
                    {
                        g0?.Dispose();
                        page = doc.AddPage();
                        page.Size = PdfSharp.PageSize.A4;
                        g0 = XGraphics.FromPdfPage(page);
                        MaybeDrawDebugGrid(g0);
                    }
                    DrawOne(g0!, flat[i], posInPage);
                }
            }
            finally { g0?.Dispose(); }
        }

        if (doc.PageCount == 0) doc.AddPage().Size = PdfSharp.PageSize.A4;

        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static void DrawGroupHeader(XGraphics g, string? header, XFont font, XBrush brush)
    {
        if (string.IsNullOrWhiteSpace(header)) return;
        var pageW = Avery3421.SheetW * MmToPt;
        var y = 5.5 * MmToPt; // 5.5mm do topo — dentro da margem de 8.8mm
        var fmt = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.BaseLine };
        g.DrawString(header, font, brush, new XPoint(pageW / 2.0, y), fmt);
    }

    // ── Desenho de uma etiqueta ─────────────────────────────────────────────

    private sealed record LineSpec(XFont Font, XBrush Brush, Func<EtiquetaLabel, string> Text);

    // Posiciona as 4 linhas nas baselines calibradas (BaselinesMm), medidas
    // em mm do topo da célula. As baselines vêm do sample de referência —
    // esta escolha (em vez de distribuir uniformemente por font.GetHeight)
    // preserva o alinhamento sub-milimétrico com a folha física já em uso.
    // A cada linha, o "topo da célula" cai no bordo do autocolante.
    private static void DrawLabel(
        XGraphics g, EtiquetaLabel lab, int row, int col, LineSpec[] lines)
    {
        double innerX = Avery3421.InnerXPt(col);
        double innerW = Avery3421.InnerWpt;
        double cellTop = Avery3421.CellYPt(row);

        for (int i = 0; i < lines.Length; i++)
        {
            var spec = lines[i];
            var text = spec.Text(lab) ?? "";
            double baselineY = cellTop + BaselinesMm[i] * MmToPt;
            DrawCentered(g, text, spec.Font, spec.Brush, innerX, innerW, baselineY);
        }
    }

    private static void DrawCentered(
        XGraphics g, string? text, XFont baseFont, XBrush brush,
        double x, double w, double baselineY)
    {
        var t = text ?? "";
        if (string.IsNullOrEmpty(t)) return;

        var f = baseFont;
        var size = g.MeasureString(t, f);
        while (size.Width > w && f.Size > 5.5)
        {
            f = new XFont(FontFamily, f.Size - 0.5, f.Style);
            size = g.MeasureString(t, f);
        }

        if (size.Width > w)
        {
            t = Truncate(g, t, f, w);
            size = g.MeasureString(t, f);
        }

        double drawX = x + (w - size.Width) / 2;
        var fmt = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.BaseLine };
        g.DrawString(t, f, brush, new XPoint(drawX, baselineY), fmt);
    }

    private static string Truncate(XGraphics g, string s, XFont f, double maxW)
    {
        const string ell = "…";
        for (int len = s.Length - 1; len > 0; len--)
        {
            var candidate = s.Substring(0, len).TrimEnd() + ell;
            if (g.MeasureString(candidate, f).Width <= maxW) return candidate;
        }
        return ell;
    }

    // ── Debug grid ──────────────────────────────────────────────────────────
    // Activa com PDF_DEBUG_GRID=1 (env var). Desenha o bordo de cada célula
    // + a área interior (padding) + linhas horizontais nas baselines
    // calculadas. Útil para inspecção visual do alinhamento.
    private static void MaybeDrawDebugGrid(XGraphics? g)
    {
        if (g is null) return;
        if (Environment.GetEnvironmentVariable("PDF_DEBUG_GRID") != "1") return;

        var thin = new XPen(XColor.FromArgb(220, 220, 220), 0.2);
        var thinR = new XPen(XColor.FromArgb(255, 200, 200), 0.2);

        for (int r = 0; r < Avery3421.Rows; r++)
        for (int c = 0; c < Avery3421.Cols; c++)
        {
            var cx = Avery3421.CellXPt(c);
            var cy = Avery3421.CellYPt(r);
            g.DrawRectangle(thin, cx, cy, Avery3421.CellWpt, Avery3421.CellHpt);
            g.DrawRectangle(thinR, Avery3421.InnerXPt(c), Avery3421.InnerYPt(r),
                Avery3421.InnerWpt, Avery3421.InnerHpt);
        }
    }
}