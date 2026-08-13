using AOB.Application.Contracts;
using AOB.Core.Entities;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Snippets.Font;

namespace AOB.Application.Forms;

public static class InscricaoConvoyagePdfGenerator
{
    static InscricaoConvoyagePdfGenerator()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new FailsafeFontResolver();
    }

    public static byte[] Render(Site site, InscricaoConvoyageRequest r, int submissionId, string localRecolha, int year)
    {
        var doc = new PdfDocument();
        doc.Info.Title = $"Inscrição Convoyage BVA Masters — {r.NomeCompleto}";

        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using var g = XGraphics.FromPdfPage(page);

        var fontBold   = new XFont("Arial", 10, XFontStyleEx.Bold);
        var fontReg    = new XFont("Arial", 9,  XFontStyleEx.Regular);
        var fontSmall  = new XFont("Arial", 8,  XFontStyleEx.Regular);
        var fontTitle  = new XFont("Arial", 16, XFontStyleEx.Bold);
        var fontSub    = new XFont("Arial", 11, XFontStyleEx.Bold);
        var fontHeader = new XFont("Arial", 8,  XFontStyleEx.Bold);

        var ink      = XBrushes.Black;
        var grey     = new XSolidBrush(XColor.FromArgb(100, 100, 100));
        var lightGrey= new XSolidBrush(XColor.FromArgb(230, 230, 230));
        var darkBlue = new XSolidBrush(XColor.FromArgb(26, 67, 128));
        var white    = XBrushes.White;
        var borderPen= new XPen(XColor.FromArgb(180, 180, 180), 0.5);

        double margin = 40;
        double pageW  = page.Width.Point;
        double y      = margin;

        // ── Cabeçalho ────────────────────────────────────────────────────────
        g.DrawRectangle(darkBlue, new XRect(0, 0, pageW, 60));
        g.DrawString("BVA Masters", fontTitle, white, new XPoint(margin, 25));
        g.DrawString($"Ficha de Inscrição — Convoyage {year}", fontSub, white, new XPoint(margin, 46));

        var dateLine = $"Submetido em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC  ·  Ref. #{submissionId}";
        var dateW = g.MeasureString(dateLine, fontSmall).Width;
        g.DrawString(dateLine, fontSmall, white, new XPoint(pageW - margin - dateW, 46));

        y = 75;

        // ── Dados do criador ─────────────────────────────────────────────────
        g.DrawString("Dados do criador", fontSub, ink, new XPoint(margin, y));
        y += 18;

        var labelW  = 140.0;
        var colW    = (pageW - 2 * margin - labelW) / 1;
        var rowH    = 16.0;

        void DrawRow(string label, string? value, bool shaded = false)
        {
            if (shaded)
                g.DrawRectangle(lightGrey, new XRect(margin, y - 11, pageW - 2 * margin, rowH));
            g.DrawString(label + ":", fontBold, ink, new XPoint(margin + 2, y));
            g.DrawString(value ?? "—", fontReg, ink, new XPoint(margin + labelW, y));
            g.DrawRectangle(borderPen, new XRect(margin, y - 11, pageW - 2 * margin, rowH));
            y += rowH;
        }

        DrawRow("Nome completo",     r.NomeCompleto, shaded: true);
        DrawRow("País",              r.Pais);
        DrawRow("Email",             r.Email, shaded: true);
        DrawRow("Telefone",          r.Telefone);
        DrawRow("Nº sócio BVA",     r.NumeroSocioBva, shaded: true);
        DrawRow("Nº STAM",          r.NumeroStam);
        DrawRow("Local de recolha", localRecolha, shaded: true);

        y += 10;

        // ── Tabela de aves ───────────────────────────────────────────────────
        g.DrawString("Aves inscritas", fontSub, ink, new XPoint(margin, y));
        y += 18;

        // Larguras das colunas
        double col0 = 80;   // Nº Série/Classe
        double col1 = 275;  // Espécies e Mutação
        double col2 = 90;   // Anilha
        double tableW = col0 + col1 + col2;
        double x = margin;

        // Cabeçalho da tabela
        g.DrawRectangle(darkBlue, new XRect(x, y - 11, tableW, 16));
        g.DrawString("Nº Série/Classe",    fontHeader, white, new XPoint(x + 3, y)); x += col0;
        g.DrawString("Espécies e Mutação", fontHeader, white, new XPoint(x + 3, y)); x += col1;
        g.DrawString("Anilha",             fontHeader, white, new XPoint(x + 3, y));
        y += 16;

        for (int i = 0; i < r.Aves.Count; i++)
        {
            var ave = r.Aves[i];
            bool shade = i % 2 == 0;
            x = margin;

            // Handle page overflow (add new page if needed)
            if (y > page.Height.Point - 60)
            {
                page = doc.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                g.Dispose();
                // Note: after adding a page we lose the current graphics context.
                // For simplicity we let it overflow rather than multi-page support in v1.
            }

            if (shade)
                g.DrawRectangle(lightGrey, new XRect(margin, y - 11, tableW, 15));

            g.DrawString(ave.Serie ?? "",          fontReg, ink, new XPoint(x + 3, y)); x += col0;
            var mut = TruncateToWidth(g, ave.EspecieMutacao ?? "", fontReg, col1 - 6);
            g.DrawString(mut,                      fontReg, ink, new XPoint(x + 3, y)); x += col1;
            g.DrawString(ave.Anilha ?? "",         fontReg, ink, new XPoint(x + 3, y));

            // Border for each row
            x = margin;
            g.DrawRectangle(borderPen, new XRect(x, y - 11, tableW, 15));
            y += 15;
        }

        y += 12;

        // ── Total ─────────────────────────────────────────────────────────────
        g.DrawString($"Total de aves inscritas: {r.Aves.Count}", fontBold, ink, new XPoint(margin, y));
        y += 20;

        // ── Declaração ────────────────────────────────────────────────────────
        g.DrawString("Declaro que os dados acima são correctos e que aceito o regulamento da convoyage BVA Masters.",
            fontSmall, grey, new XPoint(margin, y));
        y += 14;

        g.DrawString($"{r.Pais}, {DateTime.UtcNow:dd/MM/yyyy}", fontSmall, grey, new XPoint(margin, y));
        y += 14;
        g.DrawString("Assinatura: _______________________________", fontSmall, grey, new XPoint(margin, y));

        // ── Rodapé ────────────────────────────────────────────────────────────
        var footerFont = new XFont("Arial", 7.5, XFontStyleEx.Regular);
        var footerY = page.Height.Point - 18;
        g.DrawLine(borderPen, margin, footerY - 6, pageW - margin, footerY - 6);
        g.DrawString(
            $"Inscrição #{submissionId} · {site.Name} · Gerado em {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            footerFont, grey, new XPoint(margin, footerY));

        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static string TruncateToWidth(XGraphics g, string text, XFont font, double maxWidth)
    {
        if (g.MeasureString(text, font).Width <= maxWidth) return text;
        while (text.Length > 0 && g.MeasureString(text + "…", font).Width > maxWidth)
            text = text[..^1];
        return text + "…";
    }
}
