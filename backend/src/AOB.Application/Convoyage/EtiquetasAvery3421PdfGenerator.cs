using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Snippets.Font;

namespace AOB.Application.Convoyage;

// Gera um PDF com etiquetas Avery 3421 (Zweckform 3421):
//   A4, 3 colunas × 11 linhas = 33 etiquetas por folha, 70mm × 25.4mm cada,
//   margem superior 8.8mm, sem margens laterais, sem separações.
// Cada etiqueta tem 4 linhas (Helvetica, para casar com o sample ReportLab):
//   1) Anilha (Helvetica 7.2pt regular, preto)
//   2) Descrição — mutação/espécie OU "Entregar a: X" (Helvetica 7.5pt regular, preto)
//   3) Rótulo — série "003/04" OU "TRANSPORTE" OU "VENDAS" (Helvetica-Bold 8.5pt, vermelho)
//   4) Nome do criador em maiúsculas (Helvetica-Bold 8.5pt, preto)
//
// Coordenadas (baselines das 4 linhas na row 1, em mm do topo A4) foram
// extraídas do sample fornecido pelo utilizador
// (_local/examples/etiquetas_PACOS_DE_FERREIRA_avery3421_25.4mm.pdf) para
// garantir alinhamento sub-milimétrico com a folha física já em uso.
public static class EtiquetasAvery3421PdfGenerator
{
    static EtiquetasAvery3421PdfGenerator()
    {
        // Usamos um resolver dedicado que carrega Arial real do sistema —
        // metricamente idêntico ao Helvetica do sample. Se já houver um
        // resolver global (definido pelos outros geradores do projecto), fazemos
        // wrap com fallback para o existente.
        var existing = GlobalFontSettings.FontResolver;
        if (existing is not ArialFontResolver and not ArialWithFallbackResolver)
        {
            GlobalFontSettings.FontResolver = existing is null
                ? (IFontResolver)new ArialFontResolver()
                : new ArialWithFallbackResolver(new ArialFontResolver(), existing);
        }
    }

    // Encadeia dois resolvers: primeiro tenta Arial, senão delega no fallback.
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

    /// Uma etiqueta física a imprimir.
    /// <param name="Nome">Nome do criador (será posto em MAIÚSCULAS).</param>
    /// <param name="LinhaDescricao">Descrição (EspecieMutacao ou "Entregar a: …").</param>
    /// <param name="LinhaTipoOuSerie">Série ("003/04") ou tipo ("TRANSPORTE"/"VENDAS").</param>
    /// <param name="Anilha">Número de anilha, tal como declarado.</param>
    public record EtiquetaLabel(
        string Nome,
        string Anilha,
        string LinhaDescricao,
        string LinhaTipoOuSerie,
        EtiquetaTipo Tipo);

    // ── Layout Avery 3421 (mm → pt) ─────────────────────────────────────────
    private const double MmToPt = 72.0 / 25.4;
    private const int Cols = 3;
    private const int Rows = 11;
    // "Arial" em vez de "Helvetica" porque o FailsafeFontResolver do PDFsharp
    // mapeia "Helvetica" para Segoe WP em Windows (metricamente incompatível
    // com o Helvetica do sample). Arial é o clone Microsoft do Helvetica com
    // métricas idênticas — usar Arial dá o mesmo posicionamento visual.
    private const string FontFamily = "Arial";
    private static readonly double LabelWpt = 70.0  * MmToPt; // 198.425 pt
    private static readonly double LabelHpt = 25.4  * MmToPt; //  72.000 pt

    // Baselines em pontos (do topo da célula) — reproduzem o layout do sample
    // ReportLab. A célula 1 (topo) começa a 8.8mm da margem superior da A4,
    // com baselines a 17.41 / 20.59 / 24.11 / 27.64 mm do topo → offsets
    // relativos ao topo da célula (célula em [8.8mm, 34.2mm]) de:
    //   line 1  8.61 mm  = 24.40 pt
    //   line 2 11.79 mm  = 33.41 pt
    //   line 3 15.31 mm  = 43.39 pt
    //   line 4 18.84 mm  = 53.40 pt
    private static readonly double CellTopPt   = 8.8 * MmToPt; //  24.945 pt
    private static readonly double Baseline1Pt = (17.41 - 8.8) * MmToPt;
    private static readonly double Baseline2Pt = (20.59 - 8.8) * MmToPt;
    private static readonly double Baseline3Pt = (24.11 - 8.8) * MmToPt;
    private static readonly double Baseline4Pt = (27.64 - 8.8) * MmToPt;

    // Padding horizontal só para o algoritmo de shrink-to-fit (texto é sempre
    // centrado, não alinhado à margem — mas isto evita bater no bordo da
    // etiqueta se ficar muito longo).
    private const double PadX = 4.0;

    // Font sizes extraídos do sample.
    private const double SizeAnilha = 7.2;
    private const double SizeDesc   = 7.5;
    private const double SizeTipo   = 8.5;
    private const double SizeNome   = 8.5;

    public static byte[] Render(IEnumerable<EtiquetaLabel> labels)
    {
        var list = labels?.ToList() ?? new List<EtiquetaLabel>();
        var doc = new PdfDocument();
        doc.Info.Title = "Etiquetas Convoyage";

        // Helvetica → em Windows/Linux modernos o FailsafeFontResolver mapeia
        // para Arial (que é metricamente compatível com Helvetica para os
        // caracteres latinos usados aqui).
        var fontAnilha = new XFont(FontFamily, SizeAnilha, XFontStyleEx.Regular);
        var fontDesc   = new XFont(FontFamily, SizeDesc,   XFontStyleEx.Regular);
        var fontTipo   = new XFont(FontFamily, SizeTipo,   XFontStyleEx.Bold);
        var fontNome   = new XFont(FontFamily, SizeNome,   XFontStyleEx.Bold);
        var red        = new XSolidBrush(XColor.FromArgb(200, 30, 30));
        var black      = XBrushes.Black;

        PdfPage? page = null;
        XGraphics? g = null;
        int perPage = Cols * Rows;

        try
        {
            for (int i = 0; i < list.Count; i++)
            {
                int posInPage = i % perPage;
                if (posInPage == 0)
                {
                    g?.Dispose();
                    page = doc.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    g = XGraphics.FromPdfPage(page);
                }

                int row = posInPage / Cols;
                int col = posInPage % Cols;

                double x = col * LabelWpt;
                double cellTop = CellTopPt + row * LabelHpt;

                DrawLabel(g!, list[i], x, cellTop,
                    fontAnilha, fontDesc, fontTipo, fontNome, red, black);
            }
        }
        finally
        {
            g?.Dispose();
        }

        if (doc.PageCount == 0) doc.AddPage().Size = PdfSharp.PageSize.A4;

        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static void DrawLabel(
        XGraphics g, EtiquetaLabel lab,
        double cellX, double cellTop,
        XFont fontAnilha, XFont fontDesc, XFont fontTipo, XFont fontNome,
        XBrush red, XBrush black)
    {
        double areaX = cellX + PadX;
        double areaW = LabelWpt - 2 * PadX;

        double b1 = cellTop + Baseline1Pt;
        double b2 = cellTop + Baseline2Pt;
        double b3 = cellTop + Baseline3Pt;
        double b4 = cellTop + Baseline4Pt;

        DrawCentered(g, lab.Anilha,                  fontAnilha, black, areaX, areaW, b1);
        DrawCentered(g, lab.LinhaDescricao,          fontDesc,   black, areaX, areaW, b2);
        DrawCentered(g, lab.LinhaTipoOuSerie,        fontTipo,   red,   areaX, areaW, b3);
        DrawCentered(g, lab.Nome.ToUpperInvariant(), fontNome,   black, areaX, areaW, b4);
    }

    // Desenha o texto centrado horizontalmente a uma baseline dada. Se não
    // couber na largura útil, encolhe até caber (mínimo 5.5pt) para evitar
    // overflow para a etiqueta vizinha.
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

        // Se mesmo depois de encolher ainda não couber, truncar com "…".
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
}
