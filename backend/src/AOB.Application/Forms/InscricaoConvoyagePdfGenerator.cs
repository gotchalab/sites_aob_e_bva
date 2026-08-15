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

    public static byte[] Render(Site site, InscricaoConvoyageRequest r, int submissionId, string localRecolha, int year, byte[]? logoBytes = null)
    {
        var doc = new PdfDocument();
        doc.Info.Title = $"Inscrição Convoyage BVA Masters — {r.NomeCompleto}";

        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        var g = XGraphics.FromPdfPage(page);
        try
        {

        var fontBold   = new XFont("Arial", 10, XFontStyleEx.Bold);
        var fontReg    = new XFont("Arial", 9,  XFontStyleEx.Regular);
        var fontSmall  = new XFont("Arial", 8,  XFontStyleEx.Regular);
        var fontItalic = new XFont("Arial", 8,  XFontStyleEx.Italic);
        var fontTitle  = new XFont("Arial", 16, XFontStyleEx.Bold);
        var fontSub    = new XFont("Arial", 11, XFontStyleEx.Bold);
        var fontHeader = new XFont("Arial", 8,  XFontStyleEx.Bold);

        var ink      = XBrushes.Black;
        var grey     = new XSolidBrush(XColor.FromArgb(100, 100, 100));
        var lightGrey= new XSolidBrush(XColor.FromArgb(245, 247, 250));
        var darkBlue = new XSolidBrush(XColor.FromArgb(26, 67, 128));
        var amber    = new XSolidBrush(XColor.FromArgb(180, 83, 9));
        var accentBlue = new XSolidBrush(XColor.FromArgb(46, 96, 168));
        var totalBlue = new XSolidBrush(XColor.FromArgb(234, 241, 251));
        var white    = XBrushes.White;
        var borderPen= new XPen(XColor.FromArgb(220, 224, 230), 0.5);

        double margin = 40;
        double pageW  = page.Width.Point;
        double y      = margin;

        void DrawSectionTitle(string title)
        {
            g.DrawRectangle(darkBlue, new XRect(margin, y - 10, 3, 13));
            g.DrawString(title, fontSub, ink, new XPoint(margin + 9, y));
            y += 20;
        }

        void DrawTotalLine(string text)
        {
            var textW = g.MeasureString(text, fontBold).Width;
            g.DrawLine(new XPen(XColor.FromArgb(200, 205, 215), 0.7),
                pageW - margin - textW - 4, y - 12,
                pageW - margin, y - 12);
            g.DrawString(text, fontBold, ink,
                new XPoint(pageW - margin - textW, y));
            y += 22;
        }

        // ── Cabeçalho ────────────────────────────────────────────────────────
        double headerH = 60;
        g.DrawRectangle(darkBlue, new XRect(0, 0, pageW, headerH));

        double textStartX = margin;
        if (logoBytes is { Length: > 0 })
        {
            try
            {
                using var logoStream = new MemoryStream(logoBytes);
                using var logo = XImage.FromStream(logoStream);
                double logoH = headerH - 8;
                double aspect = logo.PixelWidth / (double)logo.PixelHeight;
                double logoW = logoH * aspect;
                g.DrawImage(logo, new XRect(margin, 4, logoW, logoH));
                textStartX = margin + logoW + 12;
            }
            catch { /* fallback: no logo */ }
        }

        g.DrawString("BVA Masters", fontTitle, white, new XPoint(textStartX, 25));
        g.DrawString($"Ficha de Inscrição — Convoyage {year}", fontSub, white, new XPoint(textStartX, 46));

        var dateLine = $"Submetido em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC  ·  Ref. #{submissionId}";
        var dateW = g.MeasureString(dateLine, fontSmall).Width;
        g.DrawString(dateLine, fontSmall, white, new XPoint(pageW - margin - dateW, 46));

        y = 75;

        // ── Paginação ────────────────────────────────────────────────────────
        var footerFont = new XFont("Arial", 7.5, XFontStyleEx.Regular);
        int pageNumber = 1;
        Action? currentTableHeader = null;

        void DrawPageFooter()
        {
            var footerY = page.Height.Point - 18;
            g.DrawLine(borderPen, margin, footerY - 6, pageW - margin, footerY - 6);
            g.DrawString(
                $"Inscrição #{submissionId} · {site.Name} · Gerado em {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                footerFont, grey, new XPoint(margin, footerY));
            var pageLabel = $"Página {pageNumber}";
            var pw = g.MeasureString(pageLabel, footerFont).Width;
            g.DrawString(pageLabel, footerFont, grey, new XPoint(pageW - margin - pw, footerY));
        }

        void NewPage()
        {
            DrawPageFooter();
            g.Dispose();
            page = doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            g = XGraphics.FromPdfPage(page);
            pageNumber++;
            y = margin;
        }

        // Reserve 28pt at bottom for footer + separator.
        double PageBottom() => page.Height.Point - 28;

        void EnsureRowSpace(double needed)
        {
            if (y + needed > PageBottom())
            {
                NewPage();
                currentTableHeader?.Invoke();
            }
        }

        // ── Dados do criador ─────────────────────────────────────────────────
        DrawSectionTitle("Dados do criador");

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
        var socioBvaLabel = r.SocioBvaStatus switch
        {
            SocioBvaStatus.JaSocio          => "Sócio BVA (quotas pagas)",
            SocioBvaStatus.PagaComInscricao => "Vai pagar quota BVA com esta inscrição",
            _                                => "Não sócio BVA",
        };
        DrawRow("Situação BVA",     socioBvaLabel, shaded: true);
        DrawRow("Nº STAM",          r.NumeroStam);
        DrawRow("Local de recolha", localRecolha, shaded: true);

        const string recolhaNota = "Contacta o responsável do ponto de recolha para combinar a hora de entrega das aves.";
        g.DrawRectangle(borderPen, new XRect(margin, y - 11, pageW - 2 * margin, rowH));
        g.DrawString(recolhaNota, fontItalic, amber, new XPoint(margin + labelW, y));
        y += rowH;

        y += 10;

        var avesConcurso = r.Aves ?? new List<AveConvoyageDto>();
        double tableW = pageW - 2 * margin;
        double x = margin;

        if (avesConcurso.Count > 0)
        {
            // ── Tabela de aves ───────────────────────────────────────────────────
            DrawSectionTitle("Aves para concurso");

            // Order: individuals first (in submission order), then teams grouped
            // by EquipaId with birds ordered A→D within each team.
            var individuais = avesConcurso.Where(a => a.EquipaId is null).ToList();
            var equipas = avesConcurso
                .Where(a => a.EquipaId is not null)
                .GroupBy(a => a.EquipaId!.Value)
                .Select(g => g.OrderBy(a => a.PosicaoEquipa ?? "").ToList())
                .ToList();

            // Anilha completa "AOB PT STAM 001 FNP26 5.0" precisa ~165pt em Arial 9.
            double col0 = 60;      // Nº Série
            double col3 = 165;     // Anilha
            double col2Team = 30;  // Pos. (só em equipas)

            int rowIdx = 0;

            if (individuais.Count > 0)
            {
                // Tabela para individuais: sem coluna Pos.
                double col1Indiv = tableW - col0 - col3;

                void DrawIndivHeader()
                {
                    var xh = margin;
                    g.DrawRectangle(darkBlue, new XRect(xh, y - 11, tableW, 16));
                    g.DrawString("Nº Série",           fontHeader, white, new XPoint(xh + 3, y)); xh += col0;
                    g.DrawString("Espécies e Mutação", fontHeader, white, new XPoint(xh + 3, y)); xh += col1Indiv;
                    g.DrawString("Anilha",             fontHeader, white, new XPoint(xh + 3, y));
                    y += 16;
                }

                EnsureRowSpace(16 + RowPaddingV + RowLineHeight);
                DrawIndivHeader();
                currentTableHeader = DrawIndivHeader;

                foreach (var ave in individuais)
                {
                    var mutText = ComposeSpeciesMutation(ave.Especie, ave.EspecieMutacao);
                    var mutLines = WrapToWidth(g, mutText, fontReg, col1Indiv - 6);
                    var rowH2 = RowPaddingV + mutLines.Length * RowLineHeight;
                    EnsureRowSpace(rowH2);
                    DrawAveRow(g, ave, rowIdx++, margin, y, col0, col1Indiv, 0, col3, tableW,
                        lightGrey, borderPen, fontReg, ink, showPos: false);
                    y += rowH2;
                }

                if (equipas.Count > 0) y += 6;
            }

            if (equipas.Count > 0)
            {
                // Tabela para equipas: com coluna Pos.
                double col1Team = tableW - col0 - col2Team - col3;

                void DrawEquipasHeader()
                {
                    var xh = margin;
                    g.DrawRectangle(darkBlue, new XRect(xh, y - 11, tableW, 16));
                    g.DrawString("Nº Série",           fontHeader, white, new XPoint(xh + 3, y)); xh += col0;
                    g.DrawString("Espécies e Mutação", fontHeader, white, new XPoint(xh + 3, y)); xh += col1Team;
                    g.DrawString("Pos.",               fontHeader, white, new XPoint(xh + 3, y)); xh += col2Team;
                    g.DrawString("Anilha",             fontHeader, white, new XPoint(xh + 3, y));
                    y += 16;
                }

                EnsureRowSpace(16 + RowPaddingV + RowLineHeight);
                DrawEquipasHeader();
                currentTableHeader = DrawEquipasHeader;

                foreach (var equipa in equipas)
                {
                    var first = equipa[0];
                    var titulo = $"Equipa (T) · Série {first.Serie} · {ComposeSpeciesMutation(first.Especie, first.EspecieMutacao)}";
                    var tituloLines = WrapToWidth(g, titulo, fontHeader, tableW - 8);
                    var titleH = RowPaddingV + tituloLines.Length * RowLineHeight;

                    // Evita órfã: pelo menos título + 1 linha na página actual.
                    EnsureRowSpace(titleH + RowPaddingV + RowLineHeight);
                    g.DrawRectangle(darkBlue, new XRect(margin, y - 11, tableW, titleH));
                    for (int i = 0; i < tituloLines.Length; i++)
                        g.DrawString(tituloLines[i], fontHeader, white, new XPoint(margin + 4, y + i * RowLineHeight));
                    y += titleH;

                    // Se a equipa fluir para outra página, o cabeçalho recomeça
                    // com o título desta equipa.
                    currentTableHeader = () =>
                    {
                        DrawEquipasHeader();
                        g.DrawRectangle(darkBlue, new XRect(margin, y - 11, tableW, titleH));
                        for (int i = 0; i < tituloLines.Length; i++)
                            g.DrawString(tituloLines[i], fontHeader, white, new XPoint(margin + 4, y + i * RowLineHeight));
                        y += titleH;
                    };

                    foreach (var ave in equipa)
                    {
                        var mutText = ComposeSpeciesMutation(ave.Especie, ave.EspecieMutacao);
                        var mutLines = WrapToWidth(g, mutText, fontReg, col1Team - 6);
                        var rowH2 = RowPaddingV + mutLines.Length * RowLineHeight;
                        EnsureRowSpace(rowH2);
                        DrawAveRow(g, ave, rowIdx++, margin, y, col0, col1Team, col2Team, col3, tableW,
                            lightGrey, borderPen, fontReg, ink, showPos: true);
                        y += rowH2;
                    }

                    // Após acabar a equipa, volta ao cabeçalho simples.
                    currentTableHeader = DrawEquipasHeader;
                }
            }

            currentTableHeader = null;
            y += 12;

            // ── Total ─────────────────────────────────────────────────────────────
            EnsureRowSpace(22);
            DrawTotalLine($"Total de aves para concurso: {avesConcurso.Count}");
        }

        // ── Tabela de aves para venda ────────────────────────────────────────
        if (r.AvesVenda is { Count: > 0 })
        {
            EnsureRowSpace(20 + 16 + 15);
            DrawSectionTitle("Aves para venda");

            // Colunas: Data Nasc. | Sexo | Espécie e Mutação | Preço | Anilha
            double vc0 = 65;                                  // Data Nasc.
            double vc1 = 40;                                  // Sexo
            double vc3 = 60;                                  // Preço
            double vc4 = 165;                                 // Anilha (mesma largura de concurso)
            double vc2 = tableW - vc0 - vc1 - vc3 - vc4;      // Espécie/Mutação (resto)
            double vTableW = tableW;

            void DrawVendaHeader()
            {
                var xh = margin;
                g.DrawRectangle(darkBlue, new XRect(xh, y - 11, vTableW, 16));
                g.DrawString("Data Nasc.",        fontHeader, white, new XPoint(xh + 3, y)); xh += vc0;
                g.DrawString("Sexo",              fontHeader, white, new XPoint(xh + 3, y)); xh += vc1;
                g.DrawString("Espécie e Mutação", fontHeader, white, new XPoint(xh + 3, y)); xh += vc2;
                g.DrawString("Preço",             fontHeader, white, new XPoint(xh + 3, y)); xh += vc3;
                g.DrawString("Anilha",            fontHeader, white, new XPoint(xh + 3, y));
                y += 16;
            }

            DrawVendaHeader();
            currentTableHeader = DrawVendaHeader;

            for (int i = 0; i < r.AvesVenda.Count; i++)
            {
                var av = r.AvesVenda[i];
                bool shade = i % 2 == 0;
                EnsureRowSpace(15);
                x = margin;

                if (shade)
                    g.DrawRectangle(lightGrey, new XRect(margin, y - 11, vTableW, 15));

                var data = string.IsNullOrWhiteSpace(av.DataNascimento) ? "—" : av.DataNascimento;
                var sexo = av.Sexo switch { SexoAve.Macho => "M", SexoAve.Femea => "F", _ => "Ind." };
                var esp  = TruncateToWidth(g, ComposeSpeciesMutation(av.Especie, av.EspecieMutacao), fontReg, vc2 - 6);
                var pre  = $"{av.Preco:0.00} €";

                g.DrawString(data,               fontReg, ink, new XPoint(x + 3, y)); x += vc0;
                g.DrawString(sexo,               fontReg, ink, new XPoint(x + 3, y)); x += vc1;
                g.DrawString(esp,                fontReg, ink, new XPoint(x + 3, y)); x += vc2;
                g.DrawString(pre,                fontReg, ink, new XPoint(x + 3, y)); x += vc3;
                g.DrawString(av.Anilha ?? "",    fontReg, ink, new XPoint(x + 3, y));

                x = margin;
                g.DrawRectangle(borderPen, new XRect(x, y - 11, vTableW, 15));
                y += 15;
            }

            currentTableHeader = null;
            y += 8;
            EnsureRowSpace(22);
            DrawTotalLine($"Total de aves para venda: {r.AvesVenda.Count}");
        }

        // ── Tabela de aves para transporte (compra/venda) ────────────────────
        if (r.AvesTransporte is { Count: > 0 })
        {
            EnsureRowSpace(20 + 34 + 16 + 26);
            DrawSectionTitle("Aves para transporte (compra/venda)");
            y -= 2;

            {
                var amberBg  = new XSolidBrush(XColor.FromArgb(255, 248, 225));
                var amberBar = new XSolidBrush(XColor.FromArgb(245, 166, 35));
                var fNotice  = new XFont("Arial", 9,  XFontStyleEx.Regular);
                var fNoticeB = new XFont("Arial", 9,  XFontStyleEx.Bold);
                var line1a = "Chegada às ";
                var line1b = "12h (hora belga)";
                var line1c = " — o destinatário tem de estar presente para receber as aves.";
                var line2  = "Sujeito a confirmação após o fecho das inscrições.";
                var noticeW = pageW - 2 * margin;
                var padX = 10.0;
                var lineH = 13.0;
                var noticeH = 8 + 2 * lineH;

                g.DrawRectangle(amberBg,  new XRect(margin, y - 3, noticeW, noticeH));
                g.DrawRectangle(amberBar, new XRect(margin, y - 3, 4, noticeH));

                var tx = margin + 4 + padX;
                var ty = y + 8;
                g.DrawString(line1a, fNotice,  ink, new XPoint(tx, ty));
                var w1a = g.MeasureString(line1a, fNotice).Width;
                g.DrawString(line1b, fNoticeB, ink, new XPoint(tx + w1a, ty));
                var w1b = g.MeasureString(line1b, fNoticeB).Width;
                g.DrawString(line1c, fNotice,  ink, new XPoint(tx + w1a + w1b, ty));
                g.DrawString(line2,  fNotice,  ink, new XPoint(tx, ty + lineH));

                y += noticeH + 6;
            }

            // Colunas: Origem | Espécie | Anilha | Destinatário (nome + WhatsApp)
            double tc0 = 55;                        // Origem
            double tc2 = 130;                       // Anilha
            double tc3 = 175;                       // Destinatário
            double tc1 = tableW - tc0 - tc2 - tc3;  // Espécie (resto)

            void DrawTransporteHeader()
            {
                var xh = margin;
                g.DrawRectangle(darkBlue, new XRect(xh, y - 11, tableW, 16));
                g.DrawString("Origem",            fontHeader, white, new XPoint(xh + 3, y)); xh += tc0;
                g.DrawString("Espécie",           fontHeader, white, new XPoint(xh + 3, y)); xh += tc1;
                g.DrawString("Anilha",            fontHeader, white, new XPoint(xh + 3, y)); xh += tc2;
                g.DrawString("Destinatário · WhatsApp", fontHeader, white, new XPoint(xh + 3, y));
                y += 16;
            }

            DrawTransporteHeader();
            currentTableHeader = DrawTransporteHeader;

            for (int i = 0; i < r.AvesTransporte.Count; i++)
            {
                var av = r.AvesTransporte[i];
                bool shade = i % 2 == 0;

                var destLine1 = av.DestinatarioNome ?? "";
                var destLine2 = av.DestinatarioWhatsapp ?? "";
                var destLine3 = av.DestinatarioNotas ?? "";
                var destLine1T = TruncateToWidth(g, destLine1, fontReg, tc3 - 6);
                var destLine2T = TruncateToWidth(g, destLine2, fontSmall, tc3 - 6);
                var destLine3T = TruncateToWidth(g, destLine3, fontSmall, tc3 - 6);
                bool hasNotes = !string.IsNullOrWhiteSpace(destLine3);
                var rowHTransp = hasNotes ? 37.0 : 26.0;

                EnsureRowSpace(rowHTransp);
                x = margin;

                if (shade)
                    g.DrawRectangle(lightGrey, new XRect(margin, y - 11, tableW, rowHTransp));

                var origem = av.Origem == OrigemAveTransporte.Compra ? "Compra" : "Vende";
                var especieShort = SpeciesShort.TryGetValue(av.Especie ?? "", out var sh) ? sh : (av.Especie ?? "");
                var esp = TruncateToWidth(g, especieShort, fontReg, tc1 - 6);

                g.DrawString(origem,           fontReg, ink, new XPoint(x + 3, y)); x += tc0;
                g.DrawString(esp,              fontReg, ink, new XPoint(x + 3, y)); x += tc1;
                g.DrawString(av.Anilha ?? "",  fontReg, ink, new XPoint(x + 3, y)); x += tc2;
                g.DrawString(destLine1T,       fontReg,  ink, new XPoint(x + 3, y));
                g.DrawString(destLine2T,       fontSmall, ink, new XPoint(x + 3, y + 11));
                if (hasNotes)
                    g.DrawString("Notas: " + destLine3T, fontSmall, grey, new XPoint(x + 3, y + 22));

                g.DrawRectangle(borderPen, new XRect(margin, y - 11, tableW, rowHTransp));
                y += rowHTransp;
            }

            currentTableHeader = null;
            y += 8;
            EnsureRowSpace(22);
            DrawTotalLine($"Total de aves para transporte: {r.AvesTransporte.Count}");
        }

        // ── Resumo de custos ─────────────────────────────────────────────────
        {
            var numAvesConcurso = avesConcurso.Count;
            var numAvesVenda2 = r.AvesVenda?.Count ?? 0;
            var numAvesTransporte2 = r.AvesTransporte?.Count ?? 0;
            var totalAvesConta = numAvesConcurso + numAvesVenda2;
            var c = ConvoyagePricing.Compute(numAvesConcurso, numAvesVenda2, numAvesTransporte2, r.SocioBvaStatus);
            var tarifa = ConvoyagePricing.TransportePorAve(r.SocioBva);
            var tarifaAdq = ConvoyagePricing.TransporteAdquiridaPorAve(r.SocioBva);

            EnsureRowSpace(20 + rowH + 22);
            DrawSectionTitle("Resumo de custos");
            y -= 2;

            void CostRow(string label, string value, bool shaded = false, bool bold = false)
            {
                EnsureRowSpace(rowH);
                if (shaded)
                    g.DrawRectangle(lightGrey, new XRect(margin, y - 11, pageW - 2 * margin, rowH));
                g.DrawString(label, bold ? fontBold : fontReg, ink, new XPoint(margin + 2, y));
                var vFont = bold ? fontBold : fontReg;
                var vwidth = g.MeasureString(value, vFont).Width;
                g.DrawString(value, vFont, ink, new XPoint(pageW - margin - 4 - vwidth, y));
                g.DrawRectangle(borderPen, new XRect(margin, y - 11, pageW - 2 * margin, rowH));
                y += rowH;
            }

            void TotalPagarRow(string value)
            {
                var totalRowH = 22.0;
                EnsureRowSpace(totalRowH);
                g.DrawRectangle(darkBlue, new XRect(margin, y - 13, pageW - 2 * margin, totalRowH));
                var fontTotal = new XFont("Arial", 11, XFontStyleEx.Bold);
                g.DrawString("TOTAL a pagar", fontTotal, white, new XPoint(margin + 8, y + 1));
                var vwidth = g.MeasureString(value, fontTotal).Width;
                g.DrawString(value, fontTotal, white, new XPoint(pageW - margin - 8 - vwidth, y + 1));
                y += totalRowH;
            }

            bool shadeToggle = true;
            void CostRowAuto(string label, string value, bool bold = false)
            {
                CostRow(label, value, shaded: shadeToggle, bold: bold);
                shadeToggle = !shadeToggle;
            }

            if (c.fixa > 0)
                CostRowAuto("Inscrição na exposição", $"{c.fixa:0.00} €");
            if (numAvesConcurso > 0)
                CostRowAuto($"Inscrição por ave · {numAvesConcurso} × {ConvoyagePricing.InscricaoPorAve:0.00} €",
                    $"{c.inscricoes:0.00} €");
            if (totalAvesConta > 0)
                CostRowAuto($"Aluguer de gaiola · {totalAvesConta} × {ConvoyagePricing.GaiolaPorAve:0.00} €",
                    $"{c.gaiolas:0.00} €");
            if (totalAvesConta > 0)
                CostRowAuto($"Transporte {(r.SocioBva ? "(sócio BVA)" : "(não-sócio)")} · {totalAvesConta} × {tarifa:0.00} €",
                    $"{c.transporte:0.00} €");
            if (numAvesTransporte2 > 0)
                CostRowAuto($"Transporte de aves adquiridas/cedidas {(r.SocioBva ? "(sócio BVA)" : "(não-sócio)")} · {numAvesTransporte2} × {tarifaAdq:0.00} €",
                    $"{c.transporteAdquiridas:0.00} €");
            if (r.SocioBvaStatus == SocioBvaStatus.PagaComInscricao)
                CostRowAuto("Quota BVA Portugal", $"{c.quota:0.00} €");

            y += 4;
            TotalPagarRow($"{c.total:0.00} €");

            y += 14;
        }

        // ── Aviso de pagamento ───────────────────────────────────────────────
        {
            var amberBg   = new XSolidBrush(XColor.FromArgb(255, 248, 225));
            var amberBar  = new XSolidBrush(XColor.FromArgb(245, 166, 35));
            var fontNotice = new XFont("Arial", 9, XFontStyleEx.Regular);
            var fontNoticeBold = new XFont("Arial", 9, XFontStyleEx.Bold);

            var boxW = pageW - 2 * margin;
            var boxX = margin;
            var boxTopY = y - 2;
            var padX = 10.0;
            var noticeX = boxX + 4 + padX;
            var noticeMaxW = boxW - 4 - 2 * padX;

            var label = "Pagamento: ";
            var body  = "deve ser feito no valor certo, em dinheiro, num envelope fechado, e entregue juntamente com as aves.";
            var noticeLabelW = g.MeasureString(label, fontNoticeBold).Width;
            var firstLineMaxW = noticeMaxW - noticeLabelW;

            var firstLineWords = new List<string>();
            var remaining = new List<string>();
            var words = body.Split(' ');
            var currentLine = "";
            int i;
            for (i = 0; i < words.Length; i++)
            {
                var candidate = currentLine.Length == 0 ? words[i] : currentLine + " " + words[i];
                if (g.MeasureString(candidate, fontNotice).Width > firstLineMaxW) break;
                currentLine = candidate;
                firstLineWords.Add(words[i]);
            }
            for (; i < words.Length; i++) remaining.Add(words[i]);

            var extraLines = WrapToWidth(g, string.Join(' ', remaining), fontNotice, noticeMaxW);
            var totalLines = 1 + (remaining.Count > 0 ? extraLines.Length : 0);
            var boxH = 6 + totalLines * RowLineHeight;

            EnsureRowSpace(boxH + 10);
            boxTopY = y - 2;
            g.DrawRectangle(amberBg, new XRect(boxX, boxTopY, boxW, boxH));
            g.DrawRectangle(amberBar, new XRect(boxX, boxTopY, 4, boxH));

            g.DrawString(label, fontNoticeBold, XBrushes.Black, new XPoint(noticeX, y + 8));
            g.DrawString(string.Join(' ', firstLineWords), fontNotice, XBrushes.Black,
                new XPoint(noticeX + noticeLabelW, y + 8));
            for (int li = 0; li < extraLines.Length && remaining.Count > 0; li++)
                g.DrawString(extraLines[li], fontNotice, XBrushes.Black,
                    new XPoint(noticeX, y + 8 + (li + 1) * RowLineHeight));

            y += boxH + 10;
        }

            // ── Rodapé (última página) ───────────────────────────────────────
            DrawPageFooter();

            using var ms = new MemoryStream();
            doc.Save(ms, closeStream: false);
            return ms.ToArray();
        }
        finally
        {
            g.Dispose();
        }
    }

    private static readonly Dictionary<string, string> SpeciesShort = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Roseicollis"] = "A. roseicollis",
        ["Personatus"]  = "A. personatus",
        ["Fischeri"]    = "A. fischeri",
        ["Nigrigenis"]  = "A. nigrigenis",
        ["Lilianae"]    = "A. lilianae",
        ["Canus"]       = "A. canus",
        ["Taranta"]     = "A. taranta",
        ["Pullarius"]   = "A. pullarius",
    };

    private static string ComposeSpeciesMutation(string? especie, string? mutacao)
    {
        var mut = (mutacao ?? "").Trim();
        var esp = (especie ?? "").Trim();
        if (string.IsNullOrEmpty(esp)) return mut;
        var shortName = SpeciesShort.TryGetValue(esp, out var s) ? s : esp;
        // Avoid duplication when the mutation already starts with the species short name.
        if (mut.StartsWith(shortName, StringComparison.OrdinalIgnoreCase)) return mut;
        return string.IsNullOrEmpty(mut) ? shortName : $"{shortName} · {mut}";
    }

    private static string TruncateToWidth(XGraphics g, string text, XFont font, double maxWidth)
    {
        if (g.MeasureString(text, font).Width <= maxWidth) return text;
        while (text.Length > 0 && g.MeasureString(text + "…", font).Width > maxWidth)
            text = text[..^1];
        return text + "…";
    }

    // Word-wraps text into as many lines as needed to fit maxWidth. Words that
    // are individually wider than maxWidth are hard-broken character-wise.
    private static string[] WrapToWidth(XGraphics g, string text, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return new[] { "" };
        if (g.MeasureString(text, font).Width <= maxWidth) return new[] { text };

        var words = text.Split(' ');
        var lines = new List<string>();
        var current = "";

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (g.MeasureString(candidate, font).Width <= maxWidth)
            {
                current = candidate;
                continue;
            }

            // Flush current line and start a new one with this word.
            if (current.Length > 0) lines.Add(current);

            // If the word alone doesn't fit, hard-break it character-wise.
            if (g.MeasureString(word, font).Width > maxWidth)
            {
                var chunk = "";
                foreach (var ch in word)
                {
                    var trial = chunk + ch;
                    if (g.MeasureString(trial, font).Width > maxWidth)
                    {
                        if (chunk.Length > 0) lines.Add(chunk);
                        chunk = ch.ToString();
                    }
                    else
                    {
                        chunk = trial;
                    }
                }
                current = chunk;
            }
            else
            {
                current = word;
            }
        }

        if (current.Length > 0) lines.Add(current);
        return lines.ToArray();
    }

    private const double RowLineHeight = 12.0;
    private const double RowPaddingV = 3.0;

    // Draws a single ave row with dynamic height (15pt for 1 line, 27pt for 2 lines
    // depending on how the "Espécies e Mutação" column wraps). Returns the row height.
    private static double DrawAveRow(
        XGraphics g, AveConvoyageDto ave, int rowIdx,
        double margin, double y,
        double col0, double col1, double col2, double col3, double tableW,
        XBrush lightGrey, XPen borderPen, XFont fontReg, XBrush ink,
        bool showPos)
    {
        var mutText = ComposeSpeciesMutation(ave.Especie, ave.EspecieMutacao);
        var mutLines = WrapToWidth(g, mutText, fontReg, col1 - 6);
        var rowH = RowPaddingV + mutLines.Length * RowLineHeight;

        var x = margin;
        if (rowIdx % 2 == 0)
            g.DrawRectangle(lightGrey, new XRect(margin, y - 11, tableW, rowH));

        g.DrawString(ave.Serie ?? "", fontReg, ink, new XPoint(x + 3, y)); x += col0;
        for (int i = 0; i < mutLines.Length; i++)
            g.DrawString(mutLines[i], fontReg, ink, new XPoint(x + 3, y + i * RowLineHeight));
        x += col1;
        if (showPos)
        {
            g.DrawString(ave.PosicaoEquipa ?? "—", fontReg, ink, new XPoint(x + 3, y));
            x += col2;
        }
        g.DrawString(ave.Anilha ?? "", fontReg, ink, new XPoint(x + 3, y));

        g.DrawRectangle(borderPen, new XRect(margin, y - 11, tableW, rowH));
        return rowH;
    }
}
