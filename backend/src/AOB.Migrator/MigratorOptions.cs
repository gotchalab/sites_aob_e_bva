namespace AOB.Migrator;

public class MigratorOptions
{
    public SshOptions Ssh { get; set; } = new();
    public MySqlOptions MySql { get; set; } = new();
    public JoomlaSites Joomla { get; set; } = new();
    public string LegacyMediaPath { get; set; } = "";
}

public class SshOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class MySqlOptions
{
    public string RemoteHost { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; } = 3306;
    public int LocalTunnelPort { get; set; } = 3307;
}

public class JoomlaSites
{
    public JoomlaSite Aob { get; set; } = new();
    public JoomlaSite Bva { get; set; } = new();
}

public class JoomlaSite
{
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string TablePrefix { get; set; } = "";
    public int TargetSiteId { get; set; }
}
