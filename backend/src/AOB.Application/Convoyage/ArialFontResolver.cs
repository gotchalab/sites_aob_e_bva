using System.Reflection;
using PdfSharp.Fonts;

namespace AOB.Application.Convoyage;

// Resolver dedicado para servir uma fonte metricamente compatível com
// Arial/Helvetica ao gerador Avery 3421.
//
// Estratégia: usar Liberation Sans embebida como Embedded Resource
// (LiberationSans-Regular.ttf e -Bold.ttf em Convoyage/fonts/). Liberation
// Sans é o clone SIL-OFL da Red Hat com métricas by-design idênticas a Arial,
// garantindo posicionamento consistente em qualquer OS/host — dev (Windows) e
// prod (Linux) produzem o mesmo bitmap. Elimina a dependência de
// msttcorefonts (raro) ou fallback para DejaVu (métricas incompatíveis) que
// causava shrink-to-fit em cascata e desalinhamento visual em prod.
internal sealed class ArialFontResolver : IFontResolver
{
    private const string ResRegular = "AOB.Application.Convoyage.fonts.LiberationSans-Regular.ttf";
    private const string ResBold    = "AOB.Application.Convoyage.fonts.LiberationSans-Bold.ttf";

    private static readonly Lazy<byte[]?> _regular = new(() => LoadEmbedded(ResRegular));
    private static readonly Lazy<byte[]?> _bold    = new(() => LoadEmbedded(ResBold));

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Respondemos por Arial / Helvetica / Liberation Sans — todos mapeiam
        // para a mesma fonte embebida. Outros nomes seguem para o fallback do
        // PDFsharp.
        var f = familyName ?? "";
        var match = f.Equals("Arial", StringComparison.OrdinalIgnoreCase)
                 || f.Equals("Helvetica", StringComparison.OrdinalIgnoreCase)
                 || f.Equals("Liberation Sans", StringComparison.OrdinalIgnoreCase)
                 || f.Equals("LiberationSans", StringComparison.OrdinalIgnoreCase);
        if (!match) return null;

        var faceName = isBold ? "LibSans#b" : "LibSans#";
        return new FontResolverInfo(faceName);
    }

    public byte[]? GetFont(string faceName)
    {
        var bytes = faceName.EndsWith("#b", StringComparison.Ordinal) ? _bold.Value : _regular.Value;
        // Se o embedded não estiver disponível (build corrompido) devolvemos
        // null para o PDFsharp cair no fallback e evitar crash.
        return bytes;
    }

    private static byte[]? LoadEmbedded(string resourceName)
    {
        var asm = typeof(ArialFontResolver).Assembly;
        using var s = asm.GetManifestResourceStream(resourceName);
        if (s is null) return null;
        using var ms = new MemoryStream((int)s.Length);
        s.CopyTo(ms);
        return ms.ToArray();
    }
}