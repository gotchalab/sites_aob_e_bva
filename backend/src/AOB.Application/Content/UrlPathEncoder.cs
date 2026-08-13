namespace AOB.Application.Content;

public static class UrlPathEncoder
{
    /// Percent-encode UTF-8 filenames em paths locais tipo "/uploads/aob/images/Cópia.png".
    /// Preserva "/", ignora URLs absolutas e paths já com %XX. Sem isto, o Kestrel no Windows
    /// devolve 404 para pedidos com UTF-8 raw no path.
    public static string? EncodeUploadPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return path;

        var query = "";
        var qIdx = path.IndexOf('?');
        if (qIdx >= 0)
        {
            query = path[qIdx..];
            path = path[..qIdx];
        }

        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length == 0) continue;
            segments[i] = NeedsEncoding(segments[i]) ? Uri.EscapeDataString(segments[i]) : segments[i];
        }
        return string.Join('/', segments) + query;
    }

    private static bool NeedsEncoding(string segment)
    {
        foreach (var c in segment)
        {
            if (c > 127) return true;
            if (c == ' ') return true;
        }
        return false;
    }
}
