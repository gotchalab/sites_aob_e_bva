using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace AOB.Migrator;

/// <summary>
/// Abre um túnel SSH que expõe a porta 3306 do VPS na localhost:3307 do dev.
/// </summary>
public sealed class SshTunnel : IDisposable
{
    private readonly SshClient _client;
    private readonly ForwardedPortLocal _port;
    private readonly ILogger<SshTunnel> _log;

    public SshTunnel(MigratorOptions opts, ILogger<SshTunnel> log)
    {
        _log = log;
        var conn = new ConnectionInfo(opts.Ssh.Host, opts.Ssh.Port, opts.Ssh.Username,
            new PasswordAuthenticationMethod(opts.Ssh.Username, opts.Ssh.Password));

        _client = new SshClient(conn);
        _client.Connect();
        _log.LogInformation("SSH conectado a {Host}:{Port}", opts.Ssh.Host, opts.Ssh.Port);

        _port = new ForwardedPortLocal("127.0.0.1", (uint)opts.MySql.LocalTunnelPort,
            opts.MySql.RemoteHost, (uint)opts.MySql.RemotePort);
        _client.AddForwardedPort(_port);
        _port.Start();
        _log.LogInformation("Tunel: localhost:{Local} -> {RemoteHost}:{RemotePort}",
            opts.MySql.LocalTunnelPort, opts.MySql.RemoteHost, opts.MySql.RemotePort);
    }

    public void Dispose()
    {
        try { _port.Stop(); } catch { }
        try { _client.Disconnect(); } catch { }
        _client.Dispose();
    }
}
