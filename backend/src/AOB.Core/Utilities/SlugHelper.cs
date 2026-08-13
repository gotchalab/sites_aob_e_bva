using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AOB.Core.Utilities;

public static class SlugHelper
{
    private static readonly Regex NonAlnum = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex Trim = new(@"^-+|-+$", RegexOptions.Compiled);

    public static string Slugify(string input, int maxLen = 200)
    {
        if (string.IsNullOrWhiteSpace(input)) return "sem-titulo";

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        var s = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        s = NonAlnum.Replace(s, "-");
        s = Trim.Replace(s, "");
        if (s.Length > maxLen) s = s[..maxLen].TrimEnd('-');
        return string.IsNullOrEmpty(s) ? "sem-titulo" : s;
    }
}
