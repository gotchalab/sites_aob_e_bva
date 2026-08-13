using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using AOB.Migrator;
using AOB.Migrator.Commands;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.Configure<MigratorOptions>(builder.Configuration);
builder.Services.AddSingleton(sp => sp.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<MigratorOptions>>().Value);

var pgConn = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres in appsettings");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(pgConn));

builder.Services.AddIdentityCore<ApplicationUser>(AOB.Infrastructure.DependencyInjection.ConfigureIdentity)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<SshTunnel>();
builder.Services.AddScoped<SeedCommand>();
builder.Services.AddScoped<SeedSociosCommand>();
builder.Services.AddScoped<MigrateCategoriesCommand>();
builder.Services.AddScoped<MigrateArticlesCommand>();
builder.Services.AddScoped<MigrateDownloadsCommand>();
builder.Services.AddScoped<MigrateMenusCommand>();
builder.Services.AddScoped<MigrateImagesCommand>();
builder.Services.AddScoped<MigrateSponsorsCommand>();
builder.Services.AddScoped<SeedHomeContentCommand>();
builder.Services.AddScoped<PatchBvaHomeCommand>();
builder.Services.AddScoped<GenerateRedirectsCommand>();
builder.Services.AddSingleton<ExternalImageDownloader>();
builder.Services.AddScoped<FixExternalImagesCommand>();

var host = builder.Build();

var log = host.Services.GetRequiredService<ILogger<Program>>();
var opts = host.Services.GetRequiredService<MigratorOptions>();

var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "ping";
var sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";

switch (cmd)
{
    case "ping":
        await Ping(host, log, opts);
        break;
    case "seed":
        await RunScoped<SeedCommand>(host, c => c.RunAsync());
        break;
    case "seed-socios":
        await RunScoped<SeedSociosCommand>(host, c => c.RunAsync());
        break;
    case "migrate":
        await RunMigrate(host, sub, log);
        break;
    case "redirects":
        await RunScoped<GenerateRedirectsCommand>(host, c => c.RunAsync());
        break;
    case "sponsors":
        await RunScoped<MigrateSponsorsCommand>(host, c => c.RunAsync());
        break;
    case "home-content-seed":
        await RunScoped<SeedHomeContentCommand>(host, c => c.RunAsync());
        break;
    case "patch-bva-home":
        await RunScoped<PatchBvaHomeCommand>(host, c => c.RunAsync());
        break;
    case "articles-fix-external-images":
        await RunScoped<FixExternalImagesCommand>(host, c => c.RunAsync());
        break;
    default:
        Console.WriteLine("""
            Comandos disponiveis:
              ping                    - testa conexao SSH + MariaDB VPS + Postgres local
              seed                    - insere sites e user admin base
              seed-socios             - cria sócios de teste (aob+bva) com user Identity + quotas
              migrate categories      - migra categorias (aob+bva)
              migrate articles        - migra artigos + sanitiza HTML
              migrate downloads       - migra downloads + copia ficheiros
              migrate menus           - migra estrutura de menus
              migrate all             - corre categories + articles + downloads + menus
              redirects               - gera infra/nginx/redirects.map
              sponsors                - migra banners Joomla → tabela sponsors + copia logos
              home-content-seed       - pré-popula Site.HomeConfig.mission a partir das categorias 'quem somos'
              patch-bva-home          - força actualização das áreas e CTA do site BVA na BD
              articles-fix-external-images - varre artigos, faz download de imagens externas para local e reescreve refs
            """);
        break;
}

static async Task RunMigrate(IHost host, string sub, ILogger log)
{
    switch (sub)
    {
        case "categories":
            await RunScoped<MigrateCategoriesCommand>(host, c => c.RunAsync());
            break;
        case "articles":
            await RunScoped<MigrateArticlesCommand>(host, c => c.RunAsync());
            break;
        case "downloads":
            await RunScoped<MigrateDownloadsCommand>(host, c => c.RunAsync());
            break;
        case "menus":
            await RunScoped<MigrateMenusCommand>(host, c => c.RunAsync());
            break;
        case "images":
            await RunScoped<MigrateImagesCommand>(host, c => c.RunAsync());
            break;
        case "all":
            await RunScoped<MigrateCategoriesCommand>(host, c => c.RunAsync());
            await RunScoped<MigrateArticlesCommand>(host, c => c.RunAsync());
            await RunScoped<MigrateDownloadsCommand>(host, c => c.RunAsync());
            await RunScoped<MigrateMenusCommand>(host, c => c.RunAsync());
            await RunScoped<MigrateImagesCommand>(host, c => c.RunAsync());
            break;
        default:
            log.LogError("subcomando migrate desconhecido: '{Sub}' (use categories|articles|downloads|menus|images|all)", sub);
            break;
    }
}

static async Task RunScoped<T>(IHost host, Func<T, Task> action) where T : notnull
{
    using var scope = host.Services.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<T>();
    await action(svc);
}

static async Task Ping(IHost host, ILogger log, MigratorOptions opts)
{
    log.LogInformation("=== Ping test ===");

    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var canConnect = await db.Database.CanConnectAsync();
        log.LogInformation("Postgres local: {Status}", canConnect ? "OK" : "FALHOU");
        if (canConnect)
        {
            var siteCount = await db.Sites.CountAsync();
            log.LogInformation("Sites na PG: {Count}", siteCount);
        }
    }

    using var tunnelScope = host.Services.CreateScope();
    using var tunnel = tunnelScope.ServiceProvider.GetRequiredService<SshTunnel>();

    foreach (var (name, joomla) in new[] { ("aob", opts.Joomla.Aob), ("bva", opts.Joomla.Bva) })
    {
        var cs = $"Server=127.0.0.1;Port={opts.MySql.LocalTunnelPort};" +
                 $"Database={joomla.Database};User={joomla.User};Password={joomla.Password};" +
                 "SslMode=None;AllowPublicKeyRetrieval=true;ConnectionTimeout=10";
        try
        {
            using var conn = new MySqlConnection(cs);
            await conn.OpenAsync();
            using var mycmd = conn.CreateCommand();
            mycmd.CommandText = $"SELECT COUNT(*) FROM {joomla.TablePrefix}content WHERE state=1";
            var count = Convert.ToInt32(await mycmd.ExecuteScalarAsync());
            log.LogInformation("MariaDB {Name} ({Db}): OK, {Count} artigos publicados", name, joomla.Database, count);
        }
        catch (Exception ex)
        {
            log.LogError("MariaDB {Name}: FALHOU - {Msg}", name, ex.Message);
        }
    }

    log.LogInformation("=== Fim ===");
}
