using AOB.Application.Contracts;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Snippets.Font;

namespace AOB.Application.Forms;

// Gera a "Declaração TRACES" que acompanha cada inscrição de convoyage —
// documento oficial exigido pelo Reg. Delegado (UE) 2020/688 para transporte
// intra-UE de aves de companhia (Art. 59º nº1 al. a)). Layout inspirado no
// modelo FONP: cabeçalho com logos, título "DECLARAÇÃO", parágrafo com dados
// do criador, tabela espécie/anilha, assinatura e morada FONP em rodapé.
public static class TracesDeclarationPdfGenerator
{
    static TracesDeclarationPdfGenerator()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new FailsafeFontResolver();
    }

    public static byte[] Render(
        InscricaoConvoyageRequest r,
        string campeonato,
        string matriculaTraces,
        List<(string Especie, string Anilha)> aves,
        byte[] assinaturaPng,
        byte[]? fonpLogo,
        byte[]? bvaLogo)
    {
        var doc = new PdfDocument();
        doc.Info.Title = $"Declaração TRACES — {r.NomeCompleto}";
        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var g = XGraphics.FromPdfPage(page);
        try
        {
            // Tamanhos escolhidos para replicar o modelo FONP:
            // - Header institucional "FEDERAÇÃO..." ~13pt bold
            // - Sub-header "FILIADA NA / C.O.M." ~10pt regular
            // - Título "DECLARAÇÃO" muito grande ~44pt bold
            // - Corpo do parágrafo ~12pt, tabela ~12pt (nomes/anilhas legíveis)
            var fontHeader = new XFont("Arial", 13, XFontStyleEx.Bold);
            var fontSubHeader = new XFont("Arial", 10, XFontStyleEx.Regular);
            var fontSubHeaderBold = new XFont("Arial", 10, XFontStyleEx.Bold);
            var fontTitle = new XFont("Arial", 28, XFontStyleEx.Bold);
            var fontBody = new XFont("Arial", 12, XFontStyleEx.Regular);
            var fontBodyBold = new XFont("Arial", 12, XFontStyleEx.Bold);
            var fontTable = new XFont("Arial", 10, XFontStyleEx.Regular);
            var fontTableBold = new XFont("Arial", 10, XFontStyleEx.Bold);
            var fontFooter = new XFont("Arial", 8, XFontStyleEx.Regular);

            var ink = XBrushes.Black;
            var grey = new XBrush[] { new XSolidBrush(XColor.FromArgb(90, 90, 90)) }[0];
            var borderPen = new XPen(XColor.FromArgb(0, 0, 0), 0.6);

            double margin = 50;
            double pageW = page.Width.Point;
            double pageH = page.Height.Point;
            double contentW = pageW - 2 * margin;
            double y = margin;

            // ── Cabeçalho: logos + título FONP ──────────────────────────────
            double logoH = 55;
            double headerTextX = margin;
            double headerTextRight = pageW - margin;

            if (fonpLogo is { Length: > 0 })
            {
                try
                {
                    using var s = new MemoryStream(fonpLogo);
                    using var img = XImage.FromStream(s);
                    double aspect = img.PixelWidth / (double)img.PixelHeight;
                    double logoW = logoH * aspect;
                    g.DrawImage(img, new XRect(margin, y, logoW, logoH));
                    headerTextX = margin + logoW + 12;
                }
                catch { }
            }

            if (bvaLogo is { Length: > 0 })
            {
                try
                {
                    using var s = new MemoryStream(bvaLogo);
                    using var img = XImage.FromStream(s);
                    double aspect = img.PixelWidth / (double)img.PixelHeight;
                    double logoW = logoH * aspect;
                    g.DrawImage(img, new XRect(pageW - margin - logoW, y, logoW, logoH));
                    headerTextRight = pageW - margin - logoW - 12;
                }
                catch { }
            }

            // Título FONP centrado no espaço entre os dois logos
            var titleLine1 = "FEDERAÇÃO ORNITOLÓGICA NACIONAL PORTUGUESA";
            var titleLine2 = "FILIADA NA";
            var titleLine3 = "C.O.M. – CONFÉDÉRATION ORNITHOLOGIQUE MONDIALE";

            double centerX = (headerTextX + headerTextRight) / 2;
            void DrawCenteredAt(string text, XFont f, double atY)
            {
                var w = g.MeasureString(text, f).Width;
                g.DrawString(text, f, ink, new XPoint(centerX - w / 2, atY));
            }

            DrawCenteredAt(titleLine1, fontHeader, y + 16);
            DrawCenteredAt(titleLine2, fontSubHeaderBold, y + 34);
            DrawCenteredAt(titleLine3, fontSubHeaderBold, y + 48);

            y += logoH + 30;

            // ── Título "DECLARAÇÃO" ────────────────────────────────────────
            g.DrawString("DECLARAÇÃO", fontTitle, ink, new XPoint(margin, y + 22));
            y += 50;

            // ── Parágrafo justificado (word-wrap manual) ──────────────────
            var codigoPostal = (r.CodigoPostal ?? "").Trim();
            var localidade = (r.Localidade ?? "").Trim();
            var morada = (r.Morada ?? "").Trim();
            var cpLocal = string.Join(" ", new[] { codigoPostal, localidade }.Where(x => !string.IsNullOrEmpty(x)));

            // Segmentos com estilo (bold para nome, número STAM, campeonato, matrícula).
            var segments = new List<(string text, XFont font)>
            {
                ("Eu, ", fontBody),
                (r.NomeCompleto ?? "", fontBodyBold),
                (", com morada em ", fontBody),
                (morada, fontBody),
                (string.IsNullOrEmpty(cpLocal) ? "" : ", " + cpLocal, fontBody),
                (", com o email: ", fontBody),
                (r.Email ?? "", fontBody),
                (", com o telemóvel com o número ", fontBody),
                (r.Telefone ?? "", fontBody),
                (", criador nacional registado na FONP – Federação Ornitológica Nacional Portuguesa, com o Número de Criador Nacional – ", fontBody),
                (r.NumeroStam ?? "", fontBodyBold),
                (" e inscrito no ", fontBody),
                (campeonato, fontBody),
                (", com a matrícula TRACES ", fontBody),
                (matriculaTraces, fontBody),
                (", declaro por minha honra que os pássaros por mim inscritos, e abaixo listados, cumprem os requisitos constantes na alínea a) do Nº 1 do Art. 59 do Regulamento Delegado (EU)2020/688 da Comissão, de 17 de dezembro de 2019.", fontBody),
            };

            double lineHeight = 18;
            y = DrawWrappedSegments(g, segments, margin, y, contentW, lineHeight);
            y += 24;

            // ── Tabela Espécie / Nº Anilha ─────────────────────────────────
            double tableW = contentW;
            double col1W = tableW * 0.55;
            double col2W = tableW - col1W;
            double rowH = 18;
            double headerRowH = 22;

            // Cabeçalho
            g.DrawRectangle(borderPen, margin, y, col1W, headerRowH);
            g.DrawRectangle(borderPen, margin + col1W, y, col2W, headerRowH);
            void DrawCellCentered(string txt, XFont f, double cellX, double cellW, double cellY, double cellH)
            {
                var tw = g.MeasureString(txt, f).Width;
                g.DrawString(txt, f, ink, new XPoint(cellX + (cellW - tw) / 2, cellY + cellH / 2 + 4));
            }
            DrawCellCentered("Espécie", fontTableBold, margin, col1W, y, headerRowH);
            DrawCellCentered("Nº Anilha", fontTableBold, margin + col1W, col2W, y, headerRowH);
            y += headerRowH;

            // Linhas
            foreach (var (esp, an) in aves)
            {
                if (y + rowH > pageH - 180)
                {
                    // Nova página se estourar (assinatura+rodapé ocupam ~180pt)
                    g.Dispose();
                    page = doc.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    g = XGraphics.FromPdfPage(page);
                    y = margin;
                }
                g.DrawRectangle(borderPen, margin, y, col1W, rowH);
                g.DrawRectangle(borderPen, margin + col1W, y, col2W, rowH);
                DrawCellCentered(esp, fontTable, margin, col1W, y, rowH);
                DrawCellCentered(an, fontTable, margin + col1W, col2W, y, rowH);
                y += rowH;
            }

            // ── Assinatura (empurra para baixo, deixa espaço para rodapé) ─
            double assinaturaBlockY = pageH - 170;
            if (y > assinaturaBlockY - 40) assinaturaBlockY = y + 60;

            // Área da assinatura: reservamos um rectângulo signW×signH e
            // encostamos a imagem ao FUNDO desse rectângulo (fica cola à linha).
            // Como o PNG que chega já vem cropado à bounding box dos traços no
            // frontend, o whitespace é mínimo e a assinatura fica junto à linha.
            double signW = 260;
            double signH = 55;
            double signX = margin;
            double signY = assinaturaBlockY - signH;

            if (assinaturaPng is { Length: > 0 })
            {
                try
                {
                    using var s = new MemoryStream(assinaturaPng);
                    using var img = XImage.FromStream(s);
                    double aspect = img.PixelWidth / (double)img.PixelHeight;
                    double w = signH * aspect;
                    if (w > signW) { w = signW; }
                    double h = w / aspect;
                    // Colar ao fundo do rectângulo — a base da imagem fica na
                    // linha de assinatura (0.5pt acima para não sobrepor).
                    g.DrawImage(img, new XRect(signX, signY + (signH - h) - 0.5, w, h));
                }
                catch { }
            }
            // Linha de assinatura
            g.DrawLine(new XPen(XColor.FromArgb(0, 0, 0), 0.5),
                signX, assinaturaBlockY, signX + signW, assinaturaBlockY);
            g.DrawString(r.NomeCompleto ?? "", fontBody, ink,
                new XPoint(signX, assinaturaBlockY + 16));

            // ── Rodapé FONP ────────────────────────────────────────────────
            double footerY = pageH - 60;
            g.DrawLine(new XPen(XColor.FromArgb(0, 0, 0), 0.5),
                margin, footerY - 8, pageW - margin, footerY - 8);
            g.DrawString("FEDERAÇÃO ORNITOLÓGICA NACIONAL PORTUGUESA", fontFooter, ink,
                new XPoint(margin, footerY));
            g.DrawString("RUA MANUEL SILVA FERREIRA E SA, 154", fontFooter, ink,
                new XPoint(margin, footerY + 11));
            g.DrawString("4570-012 BALASAR- PÓVOA DE VARZIM      TELEM.: 96 9011071", fontFooter, ink,
                new XPoint(margin, footerY + 22));
            g.DrawString("PORTUGAL                          SITE INTERNET: www.fonp.pt   email: fonp@hotmail.com", fontFooter, ink,
                new XPoint(margin, footerY + 33));
        }
        finally
        {
            g.Dispose();
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    // Desenha uma sequência de segmentos (cada um com a sua font, para permitir
    // bold em partes do parágrafo) com word-wrap manual e justificação
    // simples à esquerda. Retorna o próximo Y disponível.
    private static double DrawWrappedSegments(
        XGraphics g,
        List<(string text, XFont font)> segments,
        double x,
        double y,
        double maxWidth,
        double lineHeight)
    {
        var currentLine = new List<(string word, XFont font, double width, double spaceAfterWidth)>();
        double lineWidth = 0;

        void Flush()
        {
            if (currentLine.Count == 0) return;
            double cursor = x;
            for (int i = 0; i < currentLine.Count; i++)
            {
                var item = currentLine[i];
                g.DrawString(item.word, item.font, XBrushes.Black, new XPoint(cursor, y));
                cursor += item.width;
                if (i < currentLine.Count - 1) cursor += item.spaceAfterWidth;
            }
            y += lineHeight;
            currentLine.Clear();
            lineWidth = 0;
        }

        foreach (var (text, font) in segments)
        {
            if (string.IsNullOrEmpty(text)) continue;
            // Split por espaço mas preserva pontuação/junção
            var words = text.Split(' ', StringSplitOptions.None);
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                if (string.IsNullOrEmpty(word)) continue;
                double wWidth = g.MeasureString(word, font).Width;
                double sWidth = g.MeasureString(" ", font).Width;
                double addWidth = (currentLine.Count == 0 ? 0 : currentLine[^1].spaceAfterWidth) + wWidth;
                if (currentLine.Count > 0 && lineWidth + addWidth > maxWidth)
                {
                    Flush();
                }
                currentLine.Add((word, font, wWidth, sWidth));
                lineWidth += (currentLine.Count == 1 ? 0 : sWidth) + wWidth;
            }
        }

        Flush();
        return y;
    }
}
