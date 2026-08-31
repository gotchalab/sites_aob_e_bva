using PdfSharp.Fonts;

namespace AOB.Application.Convoyage;

// Resolver mínimo para servir Arial (metricamente idêntico a Helvetica) a
// partir dos ficheiros do sistema. Fallback: se não encontrar Arial (raro em
// Windows/Linux com msttcorefonts / DejaVu / Liberation), devolve null e o
// PDFsharp cai no seu FailsafeFontResolver (Segoe WP).
//
// Usado exclusivamente pelo EtiquetasAvery3421PdfGenerator porque as
// etiquetas precisam de casar visualmente com a folha física impressa em
// Arial/Helvetica. Os outros geradores de PDF do projecto continuam com o
// FailsafeFontResolver global.
internal sealed class ArialFontResolver : IFontResolver
{
    // Locais habituais para o ficheiro Arial em Windows e Linux.
    private static readonly string[] Candidates =
    {
        @"C:\Windows\Fonts\arial.ttf",
        @"C:\Windows\Fonts\ARIAL.TTF",
        "/usr/share/fonts/truetype/msttcorefonts/Arial.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    };
    private static readonly string[] CandidatesBold =
    {
        @"C:\Windows\Fonts\arialbd.ttf",
        @"C:\Windows\Fonts\ARIALBD.TTF",
        "/usr/share/fonts/truetype/msttcorefonts/Arial_Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    };

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Só respondemos por "Arial" e "Helvetica" — outros nomes seguem para
        // o fallback do PDFsharp.
        if (!string.Equals(familyName, "Arial", StringComparison.OrdinalIgnoreCase)
         && !string.Equals(familyName, "Helvetica", StringComparison.OrdinalIgnoreCase))
            return null;

        var faceName = isBold ? "Arial#b" : "Arial#";
        return new FontResolverInfo(faceName);
    }

    public byte[]? GetFont(string faceName)
    {
        var paths = faceName.EndsWith("#b", StringComparison.Ordinal) ? CandidatesBold : Candidates;
        foreach (var p in paths)
            if (File.Exists(p)) return File.ReadAllBytes(p);
        return null;
    }
}
