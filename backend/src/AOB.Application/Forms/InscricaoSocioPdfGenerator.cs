using AOB.Application.Contracts;
using AOB.Core.Entities;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;
using PdfSharp.Snippets.Font;

namespace AOB.Application.Forms;

/// <summary>
/// Preenche a proposta de admissão de sócio a partir do template PDF original.
/// O template tem AcroForm com 20 campos (texto, checkbox, radios) — preenchemos os campos
/// nativos em vez de sobrepor overlay. Foto e assinatura (que não são campos do form) são
/// desenhados por cima nas coordenadas conhecidas.
///
/// Mapeamento dos radios extraído do template:
///   E_Civil: Solteiro=/Escolha4, Casado=/Escolha1, Divorciado=/Escolha2, Viuvo=/Escolha3
///   FONP:    QueroTer=/Escolha1, Nao=/Escolha2, Sim=/Escolha3
///   T_Criador: Apoiante=/Escolha1, Criador=/Escolha4 (mutuamente exclusivo)
///   S_bva:   Nao=/Escolha1, Sim=/Escolha2
/// </summary>
public static class InscricaoSocioPdfGenerator
{
    private const string TemplateRelativePath = "Templates/inscricao-socio-template.pdf";

    static InscricaoSocioPdfGenerator()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new FailsafeFontResolver();
    }

    public static byte[] Render(Site site, InscricaoSocioRequest r, int submissionId,
        byte[]? fotoBytes = null, byte[]? assinaturaBytes = null)
    {
        var templatePath = ResolveTemplatePath();
        using var doc = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);

        // Força os visualizadores PDF a re-renderizar as aparências dos campos com os
        // novos valores (sem isto, o Adobe pode mostrar o form vazio até clicar num campo).
        var form = doc.AcroForm;
        if (form is not null)
        {
            if (!form.Elements.ContainsKey("/NeedAppearances"))
                form.Elements.Add("/NeedAppearances", new PdfBoolean(true));
            else
                form.Elements["/NeedAppearances"] = new PdfBoolean(true);

            // ---- Text fields ----
            SetText(form, "Nome Completo", r.NomeCompleto);
            SetText(form, "Cartão de CidadãoBI", r.CartaoCidadao);
            SetText(form, "N Contribuinte", r.NIF);
            SetText(form, "Nacionalidade", r.Nacionalidade);
            SetText(form, "Data de Nascimento", r.DataNascimento?.ToString("dd/MM/yyyy"));
            SetText(form, "Telefone", r.Telefone);
            SetText(form, "Correio Electrónico", r.Email);
            SetText(form, "Morada 1", r.Morada);
            SetText(form, "Morada 2", r.MoradaLinha2);
            SetText(form, "Código Postal", r.CodigoPostal);
            SetText(form, "undefined", r.Localidade); // campo "Localidade" ficou sem nome no template
            SetText(form, "Profissão", r.Profissao);
            SetText(form, "STAM", r.StamFonpNumero);
            SetText(form, "STAM BVA", r.StamBvaNumero);
            SetText(form, "Data", DateTime.Now.ToString("dd/MM/yyyy"));

            // ---- Radios ----
            SetRadio(form, "E_Civil", r.EstadoCivil switch
            {
                EstadoCivilOpt.Solteiro => "/Escolha4",
                EstadoCivilOpt.Casado => "/Escolha1",
                EstadoCivilOpt.Divorciado => "/Escolha2",
                EstadoCivilOpt.Viuvo => "/Escolha3",
                _ => null,
            });
            SetRadio(form, "FONP", r.StamFonp switch
            {
                StamStatus.QueroTer => "/Escolha1",
                StamStatus.Nao => "/Escolha2",
                StamStatus.Sim => "/Escolha3",
                _ => null,
            });
            // T_Criador é radio exclusivo — se ambos marcados no form, prioriza Criador
            SetRadio(form, "T_Criador",
                r.SocioCriador ? "/Escolha4"
                : r.SocioApoiante ? "/Escolha1"
                : null);
            SetRadio(form, "S_bva", r.StamBva switch
            {
                StamStatus.Nao => "/Escolha1",
                StamStatus.Sim => "/Escolha2",
                _ => null,
            });

            // ---- Checkbox ----
            SetCheckbox(form, "Socio BVA Portugal", r.SocioBvaPortugal);
        }

        // ---- Overlay para o que NÃO é campo do form: foto, assinatura, rodapé ----
        var page = doc.Pages[0];
        using (var g = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
        {
            if (fotoBytes is not null && fotoBytes.Length > 0)
                DrawImage(g, fotoBytes, new XRect(497, 45, 80, 100));

            if (assinaturaBytes is not null && assinaturaBytes.Length > 0)
                DrawImage(g, assinaturaBytes, new XRect(231, 595, 345, 35));

            var footerFont = new XFont("Arial", 8.5, XFontStyleEx.Regular);
            var grey = new XSolidBrush(XColor.FromArgb(140, 140, 140));
            g.DrawString($"Submissao #{submissionId} · {DateTime.Now:yyyy-MM-dd HH:mm}",
                footerFont, grey, new XPoint(page.Width.Point - 250, page.Height.Point - 15));
        }

        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static void SetText(PdfAcroForm form, string fieldName, string? value)
    {
        var field = form.Fields[fieldName];
        if (field is null || value is null) return;
        if (field is PdfTextField tf)
            tf.Value = new PdfString(value);
    }

    private static void SetRadio(PdfAcroForm form, string fieldName, string? onValue)
    {
        var field = form.Fields[fieldName];
        if (field is null || onValue is null) return;
        // O radio guarda o valor selecionado como PdfName. As kids acendem quando o /AS
        // delas bate com o /V do parent.
        field.Elements["/V"] = new PdfName(onValue);
        // Propaga para as kids (obriga re-render correcto no Adobe)
        if (field.Elements["/Kids"] is PdfArray kids)
        {
            foreach (var kidRef in kids)
            {
                var kid = (kidRef as PdfReference)?.Value as PdfDictionary
                       ?? kidRef as PdfDictionary;
                if (kid is null) continue;
                // /AP/N tem uma entrada por estado. Se a kid tem a entrada onValue → esta é a selecionada.
                var apN = (kid.Elements["/AP"] as PdfDictionary)?.Elements["/N"] as PdfDictionary;
                var isSelected = apN?.Elements.ContainsKey(onValue) == true;
                kid.Elements["/AS"] = new PdfName(isSelected ? onValue : "/Off");
            }
        }
    }

    private static void SetCheckbox(PdfAcroForm form, string fieldName, bool checkedValue)
    {
        var field = form.Fields[fieldName];
        if (field is null) return;
        // Descobrir o "on" value: procurar em /AP/N a chave que NÃO é /Off.
        string onName = "/Yes";
        var apN = (field.Elements["/AP"] as PdfDictionary)?.Elements["/N"] as PdfDictionary;
        if (apN is not null)
        {
            foreach (var key in apN.Elements.Keys)
            {
                if (key != "/Off") { onName = key; break; }
            }
        }
        var value = checkedValue ? onName : "/Off";
        field.Elements["/V"] = new PdfName(value);
        field.Elements["/AS"] = new PdfName(value);
    }

    private static void DrawImage(XGraphics g, byte[] bytes, XRect rect)
    {
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            var img = XImage.FromStream(ms);
            var ratio = (double)img.PixelWidth / img.PixelHeight;
            var targetRatio = rect.Width / rect.Height;
            double w, h;
            if (ratio > targetRatio) { w = rect.Width; h = rect.Width / ratio; }
            else                     { h = rect.Height; w = rect.Height * ratio; }
            var x = rect.X + (rect.Width - w) / 2;
            var y = rect.Y + (rect.Height - h) / 2;
            g.DrawImage(img, x, y, w, h);
        }
        catch { }
    }

    private static string ResolveTemplatePath()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, TemplateRelativePath);
        if (File.Exists(beside)) return beside;
        var cwd = Path.Combine(Directory.GetCurrentDirectory(), TemplateRelativePath);
        if (File.Exists(cwd)) return cwd;
        throw new FileNotFoundException(
            $"Template PDF de inscricao nao encontrado. Procurado em: '{beside}' e '{cwd}'.");
    }
}
