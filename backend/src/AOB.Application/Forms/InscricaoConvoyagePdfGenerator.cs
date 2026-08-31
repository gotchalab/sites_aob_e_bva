using AOB.Application.Contracts;
using AOB.Core.Entities;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Snippets.Font;

namespace AOB.Application.Forms;

public enum PdfLang { Pt, En }

public static class InscricaoConvoyagePdfGenerator
{
    static InscricaoConvoyagePdfGenerator()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new FailsafeFontResolver();
    }

    // Traduções agrupadas num record para facilitar leitura e evitar
    // dicionários com chaves soltas. Adicionar aqui novo campo obriga a
    // definir os dois idiomas — o compilador impede esquecimentos.
    private sealed record Loc(
        string FormTitle, string RefLine, string PageLabel, string GeneratedFooter,
        string CreatorData, string FullName, string Country, string Email, string Phone,
        string BvaStatus, string BvaMember, string BvaPayWithReg, string BvaNonMember,
        string StamNumber, string CollectionPoint, string CollectionPointNote,
        string BirdsForContest, string SeriesNumber, string SpeciesMutation, string Ring, string Pos,
        string TeamPrefix, string TotalContestBirds,
        string BirdsForSale, string BirthDate, string Sex, string Price, string TotalSaleBirds,
        string BirdsForTransport,
        string Origin, string Species,
        string TransportRecipientHeader, string TransportSenderHeader, string TransportContactHeader,
        string NotesPrefix,
        string Buy, string Sell, string TotalTransportBirds,
        string SexMale, string SexFemale, string SexUndef,
        string CostsSummary, string ExpoRegistration, string PerBird, string CageRental,
        string TransportLabel, string TransportSocio, string TransportNonSocio,
        string AcquiredTransport, string BvaFee, string TotalToPay,
        string PaymentLabel, string PaymentBody);

    private static Loc L(PdfLang lang) => lang == PdfLang.En
        ? new Loc(
            FormTitle: "Registration Form — Convoyage {0}",
            RefLine: "Submitted on {0} UTC  ·  Ref. #{1}",
            PageLabel: "Page {0}",
            GeneratedFooter: "Registration #{0} · {1} · Generated on {2} UTC",
            CreatorData: "Breeder details",
            FullName: "Full name", Country: "Country", Email: "Email", Phone: "Phone",
            BvaStatus: "BVA status",
            BvaMember: "BVA member (dues paid)",
            BvaPayWithReg: "Paying BVA membership with this entry",
            BvaNonMember: "Non-member",
            StamNumber: "STAM number",
            CollectionPoint: "Collection point",
            CollectionPointNote: "Please contact the collection point manager to schedule bird drop-off.",
            BirdsForContest: "Show birds",
            SeriesNumber: "Series No.", SpeciesMutation: "Species and mutation", Ring: "Ring", Pos: "Pos.",
            TeamPrefix: "Team (T) · Series {0} · {1}",
            TotalContestBirds: "Total show birds: {0}",
            BirdsForSale: "Birds for sale",
            BirthDate: "Hatch date", Sex: "Sex", Price: "Price",
            TotalSaleBirds: "Total birds for sale: {0}",
            BirdsForTransport: "Transported birds (purchase / sale)",
            Origin: "Direction", Species: "Species",
            TransportRecipientHeader: "Recipient in Belgium",
            TransportSenderHeader: "Sender in Belgium",
            TransportContactHeader: "Recipient / Sender in Belgium",
            NotesPrefix: "Notes: ",
            Buy: "Purchase", Sell: "Sale",
            TotalTransportBirds: "Total transported birds: {0}",
            SexMale: "M", SexFemale: "F", SexUndef: "?",
            CostsSummary: "Cost summary",
            ExpoRegistration: "Show entry",
            PerBird: "Entry fee per bird · {0} × {1:0.00} €",
            CageRental: "Cage rental · {0} × {1:0.00} €",
            TransportLabel: "Transport {0} · {1} × {2:0.00} €",
            TransportSocio: "(BVA member rate)",
            TransportNonSocio: "(non-member rate)",
            AcquiredTransport: "Transport of purchased / delivered birds {0} · {1} space(s) × {2:0.00} €",
            BvaFee: "BVA Portugal membership",
            TotalToPay: "TOTAL to pay",
            PaymentLabel: "Payment: ",
            PaymentBody: "must be made in cash for the exact amount, sealed in an envelope and handed over together with the birds.")
        : new Loc(
            FormTitle: "Ficha de Inscrição — Convoyage {0}",
            RefLine: "Submetido em {0} UTC  ·  Ref. #{1}",
            PageLabel: "Página {0}",
            GeneratedFooter: "Inscrição #{0} · {1} · Gerado em {2} UTC",
            CreatorData: "Dados do criador",
            FullName: "Nome completo", Country: "País", Email: "Email", Phone: "Telefone",
            BvaStatus: "Situação BVA",
            BvaMember: "Sócio BVA (quotas pagas)",
            BvaPayWithReg: "Vai pagar quota BVA com esta inscrição",
            BvaNonMember: "Não sócio BVA",
            StamNumber: "Nº STAM",
            CollectionPoint: "Local de recolha",
            CollectionPointNote: "Contacta o responsável do ponto de recolha para combinar a hora de entrega das aves.",
            BirdsForContest: "Aves para concurso",
            SeriesNumber: "Nº Série", SpeciesMutation: "Espécies e Mutação", Ring: "Anilha", Pos: "Pos.",
            TeamPrefix: "Equipa (T) · Série {0} · {1}",
            TotalContestBirds: "Total de aves para concurso: {0}",
            BirdsForSale: "Aves para venda",
            BirthDate: "Data Nasc.", Sex: "Sexo", Price: "Preço",
            TotalSaleBirds: "Total de aves para venda: {0}",
            BirdsForTransport: "Aves para transporte (compra/venda)",
            Origin: "Origem", Species: "Espécie",
            TransportRecipientHeader: "Destinatário na Bélgica",
            TransportSenderHeader: "Remetente na Bélgica",
            TransportContactHeader: "Destinatário / Remetente na Bélgica",
            NotesPrefix: "Notas: ",
            Buy: "Compra", Sell: "Vende",
            TotalTransportBirds: "Total de aves para transporte: {0}",
            SexMale: "M", SexFemale: "F", SexUndef: "Ind.",
            CostsSummary: "Resumo de custos",
            ExpoRegistration: "Inscrição na exposição",
            PerBird: "Inscrição por ave · {0} × {1:0.00} €",
            CageRental: "Aluguer de gaiola · {0} × {1:0.00} €",
            TransportLabel: "Transporte {0} · {1} × {2:0.00} €",
            TransportSocio: "(sócio BVA)",
            TransportNonSocio: "(não-sócio)",
            AcquiredTransport: "Transporte de aves adquiridas/cedidas {0} · {1} espaço(s) × {2:0.00} €",
            BvaFee: "Quota BVA Portugal",
            TotalToPay: "TOTAL a pagar",
            PaymentLabel: "Pagamento: ",
            PaymentBody: "deve ser feito no valor certo, em dinheiro, num envelope fechado, e entregue juntamente com as aves.");

    public static byte[] Render(
        Site site,
        InscricaoConvoyageRequest r,
        int submissionId,
        string localRecolha,
        int year,
        byte[]? logoBytes = null,
        PdfLang lang = PdfLang.Pt,
        bool includeCosts = true,
        // Aves oferecidas (isentas de pagamento) marcadas pelo admin ao editar.
        // Descontadas dos cálculos de gaiola/transporte no resumo de custos.
        int numAvesVendaOferecidas = 0,
        int numAvesTransporteOferecidas = 0,
        // Quando false, omite completamente a secção "Aves para transporte"
        // (título + aviso âmbar + tabela + total). Útil para enviar a ficha
        // a parceiros que não estão envolvidos no transporte compra/venda.
        bool includeTransport = true)
    {
        var t = L(lang);
        var doc = new PdfDocument();
        doc.Info.Title = lang == PdfLang.En
            ? $"Convoyage BVA Masters Registration — {r.NomeCompleto}"
            : $"Inscrição Convoyage BVA Masters — {r.NomeCompleto}";

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
        g.DrawString(string.Format(t.FormTitle, year), fontSub, white, new XPoint(textStartX, 46));

        var dateLine = string.Format(t.RefLine, DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm"), submissionId);
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
                string.Format(t.GeneratedFooter, submissionId, site.Name, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")),
                footerFont, grey, new XPoint(margin, footerY));
            var pageLabel = string.Format(t.PageLabel, pageNumber);
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
        DrawSectionTitle(t.CreatorData);

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

        DrawRow(t.FullName,          r.NomeCompleto, shaded: true);
        DrawRow(t.Country,            r.Pais);
        DrawRow(t.Email,              r.Email, shaded: true);
        DrawRow(t.Phone,              r.Telefone);
        var socioBvaLabel = r.SocioBvaStatus switch
        {
            SocioBvaStatus.JaSocio          => t.BvaMember,
            SocioBvaStatus.PagaComInscricao => t.BvaPayWithReg,
            _                                => t.BvaNonMember,
        };
        DrawRow(t.BvaStatus,         socioBvaLabel, shaded: true);
        DrawRow(t.StamNumber,        r.NumeroStam);
        DrawRow(t.CollectionPoint,   localRecolha, shaded: true);

        g.DrawRectangle(borderPen, new XRect(margin, y - 11, pageW - 2 * margin, rowH));
        g.DrawString(t.CollectionPointNote, fontItalic, amber, new XPoint(margin + labelW, y));
        y += rowH;

        y += 10;

        var avesConcurso = r.Aves ?? new List<AveConvoyageDto>();
        double tableW = pageW - 2 * margin;
        double x = margin;

        if (avesConcurso.Count > 0)
        {
            // ── Tabela de aves ───────────────────────────────────────────────────
            DrawSectionTitle(t.BirdsForContest);

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
                    g.DrawString(t.SeriesNumber,       fontHeader, white, new XPoint(xh + 3, y)); xh += col0;
                    g.DrawString(t.SpeciesMutation,    fontHeader, white, new XPoint(xh + 3, y)); xh += col1Indiv;
                    g.DrawString(t.Ring,               fontHeader, white, new XPoint(xh + 3, y));
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
                    g.DrawString(t.SeriesNumber,       fontHeader, white, new XPoint(xh + 3, y)); xh += col0;
                    g.DrawString(t.SpeciesMutation,    fontHeader, white, new XPoint(xh + 3, y)); xh += col1Team;
                    g.DrawString(t.Pos,                fontHeader, white, new XPoint(xh + 3, y)); xh += col2Team;
                    g.DrawString(t.Ring,               fontHeader, white, new XPoint(xh + 3, y));
                    y += 16;
                }

                EnsureRowSpace(16 + RowPaddingV + RowLineHeight);
                DrawEquipasHeader();
                currentTableHeader = DrawEquipasHeader;

                foreach (var equipa in equipas)
                {
                    var first = equipa[0];
                    var titulo = string.Format(t.TeamPrefix, first.Serie, ComposeSpeciesMutation(first.Especie, first.EspecieMutacao));
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
            DrawTotalLine(string.Format(t.TotalContestBirds, avesConcurso.Count));
        }

        // ── Tabela de aves para venda ────────────────────────────────────────
        if (r.AvesVenda is { Count: > 0 })
        {
            EnsureRowSpace(20 + 16 + 15);
            DrawSectionTitle(t.BirdsForSale);

            // Colunas: Data Nasc. | Sexo | Espécie e Mutação | Preço | Anilha
            double vc0 = 65;                                  // Data Nasc.
            double vc1 = 40;                                  // Sexo
            double vc3 = 60;                                  // Preço
            // Aqui a anilha é um código curto do criador (ex.: "156P 08 2025")
            // — 95pt chegam de sobra e libertam espaço para a mutação.
            double vc4 = 95;                                  // Anilha
            double vc2 = tableW - vc0 - vc1 - vc3 - vc4;      // Espécie/Mutação (resto)
            double vTableW = tableW;

            void DrawVendaHeader()
            {
                var xh = margin;
                g.DrawRectangle(darkBlue, new XRect(xh, y - 11, vTableW, 16));
                g.DrawString(t.BirthDate,         fontHeader, white, new XPoint(xh + 3, y)); xh += vc0;
                g.DrawString(t.Sex,               fontHeader, white, new XPoint(xh + 3, y)); xh += vc1;
                g.DrawString(t.SpeciesMutation,   fontHeader, white, new XPoint(xh + 3, y)); xh += vc2;
                g.DrawString(t.Price,             fontHeader, white, new XPoint(xh + 3, y)); xh += vc3;
                g.DrawString(t.Ring,              fontHeader, white, new XPoint(xh + 3, y));
                y += 16;
            }

            DrawVendaHeader();
            currentTableHeader = DrawVendaHeader;

            for (int i = 0; i < r.AvesVenda.Count; i++)
            {
                var av = r.AvesVenda[i];
                bool shade = i % 2 == 0;

                var data = string.IsNullOrWhiteSpace(av.DataNascimento) ? "—" : av.DataNascimento;
                var sexo = av.Sexo switch { SexoAve.Macho => t.SexMale, SexoAve.Femea => t.SexFemale, _ => t.SexUndef };
                var espText = ComposeSpeciesMutation(av.Especie, av.EspecieMutacao);
                var espLines = WrapToWidth(g, espText, fontReg, vc2 - 6);
                var pre  = $"{av.Preco:0.00} €";

                var rowHVenda = RowPaddingV + espLines.Length * RowLineHeight;
                EnsureRowSpace(rowHVenda);
                x = margin;

                if (shade)
                    g.DrawRectangle(lightGrey, new XRect(margin, y - 11, vTableW, rowHVenda));

                g.DrawString(data,               fontReg, ink, new XPoint(x + 3, y)); x += vc0;
                g.DrawString(sexo,               fontReg, ink, new XPoint(x + 3, y)); x += vc1;
                for (int ln = 0; ln < espLines.Length; ln++)
                    g.DrawString(espLines[ln], fontReg, ink, new XPoint(x + 3, y + ln * RowLineHeight));
                x += vc2;
                g.DrawString(pre,                fontReg, ink, new XPoint(x + 3, y)); x += vc3;
                g.DrawString(av.Anilha ?? "",    fontReg, ink, new XPoint(x + 3, y));

                x = margin;
                g.DrawRectangle(borderPen, new XRect(x, y - 11, vTableW, rowHVenda));
                y += rowHVenda;
            }

            currentTableHeader = null;
            y += 8;
            EnsureRowSpace(22);
            DrawTotalLine(string.Format(t.TotalSaleBirds, r.AvesVenda.Count));
        }

        // ── Tabela de aves para transporte (compra/venda) ────────────────────
        if (includeTransport && r.AvesTransporte is { Count: > 0 })
        {
            var hasVende = r.AvesTransporte.Any(a => a.Origem == OrigemAveTransporte.Vende);
            var hasCompra = r.AvesTransporte.Any(a => a.Origem == OrigemAveTransporte.Compra);

            var amberBg  = new XSolidBrush(XColor.FromArgb(255, 248, 225));
            var amberBar = new XSolidBrush(XColor.FromArgb(245, 166, 35));
            var fNotice  = new XFont("Arial", 9,  XFontStyleEx.Regular);
            var fNoticeB = new XFont("Arial", 9,  XFontStyleEx.Bold);

            // Constrói cada aviso como lista de (texto, font) — permite word-wrap
            // com bold intercalado. Textos alinhados com as checkboxes do form.
            var noticeParagraphs = new List<List<(string text, XFont font)>>();
            if (hasVende)
            {
                noticeParagraphs.Add(lang == PdfLang.En
                    ? new List<(string, XFont)>
                    {
                        ("Sale birds (Portugal → Belgium): the ", fNotice),
                        ("recipient in Belgium", fNoticeB),
                        (" must be present when we arrive, scheduled for ", fNotice),
                        ("12:00 (Belgium time)", fNoticeB),
                        (", to receive the birds. Without conditions to provide water and food after that time, BVA ", fNotice),
                        ("assumes no responsibility for bird deaths", fNoticeB),
                        (".", fNotice),
                    }
                    : new List<(string, XFont)>
                    {
                        ("Aves para venda (Portugal → Bélgica): o ", fNotice),
                        ("destinatário na Bélgica", fNoticeB),
                        (" terá de estar presente na hora da nossa chegada, prevista para as ", fNotice),
                        ("12h (hora belga)", fNoticeB),
                        (", para receber as aves. Sem condições para dar água e alimentação após essa hora, a BVA ", fNotice),
                        ("não se responsabiliza por mortes", fNoticeB),
                        (".", fNotice),
                    });
            }
            if (hasCompra)
            {
                noticeParagraphs.Add(lang == PdfLang.En
                    ? new List<(string, XFont)>
                    {
                        ("Purchase birds (Belgium → Portugal): the ", fNotice),
                        ("sender in Belgium", fNoticeB),
                        (" may only deliver the birds to the convoy ", fNotice),
                        ("on Sunday morning", fNoticeB),
                        (". Without conditions to provide water and food before that time, BVA ", fNotice),
                        ("assumes no responsibility for bird deaths", fNoticeB),
                        (".", fNotice),
                    }
                    : new List<(string, XFont)>
                    {
                        ("Aves para compra (Bélgica → Portugal): o ", fNotice),
                        ("remetente na Bélgica", fNoticeB),
                        (" só pode entregar as aves à convoyage ", fNotice),
                        ("no domingo de manhã", fNoticeB),
                        (". Sem condições para dar água e alimentação antes desse momento, a BVA ", fNotice),
                        ("não se responsabiliza por mortes", fNoticeB),
                        (".", fNotice),
                    });
            }

            var noticeW = pageW - 2 * margin;
            var padX = 10.0;
            var lineH = 13.0;
            var contentMaxW = noticeW - 4 - 2 * padX;
            var paragraphGap = 5.0;

            // Pré-calcula linhas por parágrafo para dimensionar a caixa.
            var paragraphLineCounts = noticeParagraphs
                .Select(p => CountWrappedLines(g, p, contentMaxW))
                .ToArray();
            var totalLines = paragraphLineCounts.Sum();
            var noticeTextH = totalLines * lineH
                + (noticeParagraphs.Count > 1 ? (noticeParagraphs.Count - 1) * paragraphGap : 0);
            var noticeH = 12 + noticeTextH;

            EnsureRowSpace(20 + noticeH + 16 + 26);
            DrawSectionTitle(t.BirdsForTransport);
            y -= 2;

            g.DrawRectangle(amberBg,  new XRect(margin, y - 3, noticeW, noticeH));
            g.DrawRectangle(amberBar, new XRect(margin, y - 3, 4, noticeH));

            var contentX = margin + 4 + padX;
            var cursorY = y + 8;
            for (int pi = 0; pi < noticeParagraphs.Count; pi++)
            {
                cursorY = DrawWrappedSegments(g, noticeParagraphs[pi], contentX, cursorY, contentMaxW, lineH);
                if (pi < noticeParagraphs.Count - 1) cursorY += paragraphGap;
            }

            y += noticeH + 6;

            // Colunas: Origem | Espécie | Anilha | Destinatário (nome + WhatsApp)
            double tc0 = 55;                        // Origem
            double tc2 = 130;                       // Anilha
            double tc3 = 175;                       // Destinatário
            double tc1 = tableW - tc0 - tc2 - tc3;  // Espécie (resto)

            var contactHeader = (hasVende, hasCompra) switch
            {
                (true, false) => t.TransportRecipientHeader,
                (false, true) => t.TransportSenderHeader,
                _             => t.TransportContactHeader,
            };

            void DrawTransporteHeader()
            {
                var xh = margin;
                g.DrawRectangle(darkBlue, new XRect(xh, y - 11, tableW, 16));
                g.DrawString(t.Origin,     fontHeader, white, new XPoint(xh + 3, y)); xh += tc0;
                g.DrawString(t.Species,    fontHeader, white, new XPoint(xh + 3, y)); xh += tc1;
                g.DrawString(t.Ring,       fontHeader, white, new XPoint(xh + 3, y)); xh += tc2;
                g.DrawString(contactHeader, fontHeader, white, new XPoint(xh + 3, y));
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
                bool hasNotes = !string.IsNullOrWhiteSpace(destLine3);
                var notesLines = hasNotes
                    ? WrapToWidth(g, t.NotesPrefix + destLine3, fontSmall, tc3 - 6)
                    : Array.Empty<string>();
                var rowHTransp = hasNotes ? 26.0 + notesLines.Length * 11.0 : 26.0;

                EnsureRowSpace(rowHTransp);
                x = margin;

                if (shade)
                    g.DrawRectangle(lightGrey, new XRect(margin, y - 11, tableW, rowHTransp));

                var origem = av.Origem == OrigemAveTransporte.Compra ? t.Buy : t.Sell;
                var especieShort = Enum.TryParse<SpeciesCode>(av.Especie ?? "", ignoreCase: true, out var espCode)
                    ? SpeciesGenus.Short(espCode)
                    : (av.Especie ?? "");
                var esp = TruncateToWidth(g, especieShort, fontReg, tc1 - 6);

                g.DrawString(origem,           fontReg, ink, new XPoint(x + 3, y)); x += tc0;
                g.DrawString(esp,              fontReg, ink, new XPoint(x + 3, y)); x += tc1;
                g.DrawString(av.Anilha ?? "",  fontReg, ink, new XPoint(x + 3, y)); x += tc2;
                g.DrawString(destLine1T,       fontReg,  ink, new XPoint(x + 3, y));
                g.DrawString(destLine2T,       fontSmall, ink, new XPoint(x + 3, y + 11));
                for (int ln = 0; ln < notesLines.Length; ln++)
                    g.DrawString(notesLines[ln], fontSmall, grey, new XPoint(x + 3, y + 22 + ln * 11));

                g.DrawRectangle(borderPen, new XRect(margin, y - 11, tableW, rowHTransp));
                y += rowHTransp;
            }

            currentTableHeader = null;
            y += 8;
            EnsureRowSpace(22);
            DrawTotalLine(string.Format(t.TotalTransportBirds, r.AvesTransporte.Count));
        }

        // ── Resumo de custos ─────────────────────────────────────────────────
        if (includeCosts)
        {
            var numAvesConcurso = avesConcurso.Count;
            var numAvesVenda2 = r.AvesVenda?.Count ?? 0;
            // Quando a secção de transporte é omitida, também zeramos os
            // custos correspondentes para manter a ficha coerente (evita
            // linhas "Transporte X × Y€" sem lista visível de aves).
            var numAvesTransporte2 = includeTransport ? (r.AvesTransporte?.Count ?? 0) : 0;
            var numAvesTransporteCompra = includeTransport ? (r.AvesTransporte?.Count(a => a.Origem == OrigemAveTransporte.Compra) ?? 0) : 0;
            var numAvesTransporteVende = includeTransport ? (r.AvesTransporte?.Count(a => a.Origem == OrigemAveTransporte.Vende) ?? 0) : 0;
            var espacosTransporteTotais = ConvoyagePricing.EspacosTransporteAdquirido(
                numAvesTransporteCompra, numAvesTransporteVende);
            var vendaOferecidas = Math.Clamp(numAvesVendaOferecidas, 0, numAvesVenda2);
            var espacosOferecidos = Math.Clamp(numAvesTransporteOferecidas, 0, espacosTransporteTotais);
            var vendaFaturavel = numAvesVenda2 - vendaOferecidas;
            var espacosFaturaveis = espacosTransporteTotais - espacosOferecidos;
            var totalAvesConta = numAvesConcurso + vendaFaturavel;
            var c = ConvoyagePricing.Compute(numAvesConcurso, numAvesVenda2, numAvesTransporte2, r.SocioBvaStatus,
                vendaOferecidas, espacosOferecidos,
                numAvesTransporteCompra, numAvesTransporteVende);
            var tarifa = ConvoyagePricing.TransportePorAve(r.SocioBva);
            var tarifaAdq = ConvoyagePricing.TransporteAdquiridaPorAve(r.SocioBva);
            var socioLabel = r.SocioBva ? t.TransportSocio : t.TransportNonSocio;

            EnsureRowSpace(20 + rowH + 22);
            DrawSectionTitle(t.CostsSummary);
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
                g.DrawString(t.TotalToPay, fontTotal, white, new XPoint(margin + 8, y + 1));
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

            // Mostramos as linhas com valores BRUTOS (sem oferta) e a seguir a
            // linha "Oferta" com o desconto negativo — deixa o benefício visível.
            var totalAvesBruto = numAvesConcurso + numAvesVenda2;
            var gaiolasBruto = ConvoyagePricing.GaiolaPorAve * totalAvesBruto;
            var transporteBruto = tarifa * totalAvesBruto;
            var transporteAdqBruto = tarifaAdq * espacosTransporteTotais;
            var descontoOferta = (gaiolasBruto - c.gaiolas)
                                 + (transporteBruto - c.transporte)
                                 + (transporteAdqBruto - c.transporteAdquiridas);

            if (c.fixa > 0)
                CostRowAuto(t.ExpoRegistration, $"{c.fixa:0.00} €");
            if (numAvesConcurso > 0)
                CostRowAuto(string.Format(t.PerBird, numAvesConcurso, ConvoyagePricing.InscricaoPorAve),
                    $"{c.inscricoes:0.00} €");
            if (totalAvesBruto > 0)
                CostRowAuto(string.Format(t.CageRental, totalAvesBruto, ConvoyagePricing.GaiolaPorAve),
                    $"{gaiolasBruto:0.00} €");
            if (totalAvesBruto > 0)
                CostRowAuto(string.Format(t.TransportLabel, socioLabel, totalAvesBruto, tarifa),
                    $"{transporteBruto:0.00} €");
            if (espacosTransporteTotais > 0)
                CostRowAuto(string.Format(t.AcquiredTransport, socioLabel, espacosTransporteTotais, tarifaAdq),
                    $"{transporteAdqBruto:0.00} €");
            if (descontoOferta > 0)
            {
                var partes = new List<string>();
                if (vendaOferecidas > 0)
                    partes.Add(lang == PdfLang.En
                        ? $"{vendaOferecidas} sale bird{(vendaOferecidas == 1 ? "" : "s")}"
                        : $"{vendaOferecidas} ave{(vendaOferecidas == 1 ? "" : "s")} de venda");
                if (espacosOferecidos > 0)
                    partes.Add(lang == PdfLang.En
                        ? $"{espacosOferecidos} transport space{(espacosOferecidos == 1 ? "" : "s")}"
                        : $"{espacosOferecidos} espaço{(espacosOferecidos == 1 ? "" : "s")} de transporte");
                var lbl = lang == PdfLang.En
                    ? $"Offer (exempt from payment): {string.Join(" + ", partes)}"
                    : $"Oferta (isento de pagamento): {string.Join(" + ", partes)}";
                CostRowAuto(lbl, $"−{descontoOferta:0.00} €");
            }
            if (r.SocioBvaStatus == SocioBvaStatus.PagaComInscricao)
                CostRowAuto(t.BvaFee, $"{c.quota:0.00} €");

            y += 4;
            TotalPagarRow($"{c.total:0.00} €");

            y += 14;
        }

        // ── Aviso de pagamento ───────────────────────────────────────────────
        if (includeCosts)
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

            var label = t.PaymentLabel;
            var body  = t.PaymentBody;
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

    private static string ComposeSpeciesMutation(string? especie, string? mutacao)
    {
        var mut = (mutacao ?? "").Trim();
        var esp = (especie ?? "").Trim();
        if (string.IsNullOrEmpty(esp)) return mut;
        var shortName = Enum.TryParse<SpeciesCode>(esp, ignoreCase: true, out var code)
            ? SpeciesGenus.Short(code)
            : esp;
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

    // Desenha uma sequência de segmentos (texto+font) com word-wrap; permite
    // trocar de font (ex.: bold) no meio do parágrafo. Devolve o Y após a
    // última linha desenhada. Usado nos avisos amarelos.
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
            var words = text.Split(' ', StringSplitOptions.None);
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                if (string.IsNullOrEmpty(word)) continue;
                double wWidth = g.MeasureString(word, font).Width;
                double sWidth = g.MeasureString(" ", font).Width;
                double addWidth = (currentLine.Count == 0 ? 0 : currentLine[^1].spaceAfterWidth) + wWidth;
                if (currentLine.Count > 0 && lineWidth + addWidth > maxWidth)
                    Flush();
                currentLine.Add((word, font, wWidth, sWidth));
                lineWidth += (currentLine.Count == 1 ? 0 : sWidth) + wWidth;
            }
        }

        Flush();
        return y;
    }

    // Conta as linhas que resultarão do word-wrap sem desenhar. Usado para
    // pré-dimensionar caixas de aviso antes de renderizar.
    private static int CountWrappedLines(
        XGraphics g,
        List<(string text, XFont font)> segments,
        double maxWidth)
    {
        int lines = 1;
        double lineWidth = 0;
        double lastSpace = 0;

        foreach (var (text, font) in segments)
        {
            if (string.IsNullOrEmpty(text)) continue;
            var words = text.Split(' ', StringSplitOptions.None);
            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word)) continue;
                double wWidth = g.MeasureString(word, font).Width;
                double sWidth = g.MeasureString(" ", font).Width;
                double addWidth = (lineWidth == 0 ? 0 : lastSpace) + wWidth;
                if (lineWidth > 0 && lineWidth + addWidth > maxWidth)
                {
                    lines++;
                    lineWidth = wWidth;
                }
                else
                {
                    lineWidth += (lineWidth == 0 ? 0 : lastSpace) + wWidth;
                }
                lastSpace = sWidth;
            }
        }

        return lines;
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
