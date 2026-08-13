using System.Net.Sockets;
using System.Text;

namespace AOB.Admin.Services;

/// <summary>
/// Cliente TCP para clamd (protocolo INSTREAM). Se ClamAv:Host nao estiver
/// configurado, faz bypass silencioso (dev). Em prod deve apontar 127.0.0.1:3310.
/// </summary>
public class ClamAvScanner(IConfiguration config, ILogger<ClamAvScanner> log)
{
    public class MalwareDetected : Exception
    {
        public MalwareDetected(string signature) : base($"Malware detetado: {signature}") { }
    }

    public async Task ScanAsync(Stream data, CancellationToken ct = default)
    {
        var host = config["ClamAv:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            log.LogDebug("ClamAv:Host nao configurado — a fazer bypass do scan");
            return;
        }
        var port = int.TryParse(config["ClamAv:Port"], out var p) ? p : 3310;

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct);
        var stream = tcp.GetStream();

        // INSTREAM protocol: "zINSTREAM\0" then chunks "<uint32 length BE><chunk>" then "\0\0\0\0"
        var cmd = Encoding.ASCII.GetBytes("zINSTREAM\0");
        await stream.WriteAsync(cmd, ct);

        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await data.ReadAsync(buffer, ct)) > 0)
        {
            var len = new byte[4];
            len[0] = (byte)((read >> 24) & 0xFF);
            len[1] = (byte)((read >> 16) & 0xFF);
            len[2] = (byte)((read >> 8) & 0xFF);
            len[3] = (byte)(read & 0xFF);
            await stream.WriteAsync(len, ct);
            await stream.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        // Terminator
        await stream.WriteAsync(new byte[] { 0, 0, 0, 0 }, ct);

        var responseBuf = new byte[256];
        var n = await stream.ReadAsync(responseBuf, ct);
        var response = Encoding.ASCII.GetString(responseBuf, 0, n).TrimEnd('\0', '\n');
        log.LogDebug("ClamAV response: {Response}", response);

        if (response.EndsWith(": OK", StringComparison.OrdinalIgnoreCase)) return;

        var idx = response.IndexOf(" FOUND", StringComparison.OrdinalIgnoreCase);
        if (idx > 0)
        {
            var sig = response[..idx].Split(':').LastOrDefault()?.Trim() ?? "unknown";
            throw new MalwareDetected(sig);
        }

        log.LogWarning("ClamAV response inesperada: {Response}", response);
    }
}
