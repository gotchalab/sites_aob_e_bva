using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AOB.Migrator.Commands.Bootstrap;

/// <summary>
/// Encapsula ligacao aos 2 sites Joomla via SSH tunnel.
/// </summary>
public sealed class JoomlaSource : IDisposable
{
    private readonly SshTunnel _tunnel;
    private readonly MigratorOptions _opts;

    public record SiteBinding(string Name, JoomlaSite Joomla);

    public IReadOnlyList<SiteBinding> Sites { get; }

    public JoomlaSource(MigratorOptions opts, SshTunnel tunnel)
    {
        _opts = opts;
        _tunnel = tunnel;
        Sites = new[]
        {
            new SiteBinding("aob", opts.Joomla.Aob),
            new SiteBinding("bva", opts.Joomla.Bva),
        };
    }

    public MySqlConnection Open(JoomlaSite site)
    {
        var cs = $"Server=127.0.0.1;Port={_opts.MySql.LocalTunnelPort};" +
                 $"Database={site.Database};User={site.User};Password={site.Password};" +
                 "SslMode=None;AllowPublicKeyRetrieval=true;ConnectionTimeout=15;" +
                 "DefaultCommandTimeout=60";
        var conn = new MySqlConnection(cs);
        conn.Open();
        return conn;
    }

    public void Dispose() => _tunnel.Dispose();
}
