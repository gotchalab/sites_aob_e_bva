// AOB.Migrator - dois papeis:
//   1. FLUXO CORRENTE (usado no deploy):
//        db-update           - aplica migrations EF Core pendentes (chamado por deploy.py)
//        nom-rename-group    - utilitario admin ad-hoc
//        ping                - health check Postgres (+ Joomla se ainda configurado)
//   2. BOOTSTRAP-ONLY (so na primeira passagem AOB / BVA legacy -> Postgres):
//        seed, seed-socios, migrate *, redirects, sponsors, home-content-seed,
//        patch-bva-home, articles-fix-external-images, seed-nomenclature-2026
//      Implementados em AOB.Migrator.Commands.Bootstrap. NAO CORRER em prod
//      corrente - sao destrutivos ou dependem de MariaDB Joomla que ja nao
//      existe. Mantidos para referencia historica / disaster recovery.
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using AOB.Migrator;
using AOB.Migrator.Commands.Bootstrap;
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

var pgConn = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default (or Postgres) in appsettings");
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
builder.Services.AddScoped<SeedNomenclature2026Command>();

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
    case "db-update":
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            log.LogInformation("Nenhuma migração pendente.");
        }
        else
        {
            log.LogInformation("A aplicar {N} migrações: {List}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            log.LogInformation("Migrações aplicadas.");
        }
        break;
    }
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
    case "seed-nomenclature-2026":
        await RunScoped<SeedNomenclature2026Command>(host, c => c.RunAsync());
        break;
    case "nom-rename-group":
    {
        // uso: nom-rename-group "Grupo de Estudo" "Study Group"
        if (args.Length < 3)
        {
            log.LogError("Uso: nom-rename-group <nome-actual> <novo-nome>");
            break;
        }
        var fromName = args[1];
        var toName = args[2];
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var groups = await db.NomenclatureGroups
            .Where(g => g.DisplayName == fromName)
            .ToListAsync();
        foreach (var g in groups) g.DisplayName = toName;
        var affected = await db.SaveChangesAsync();
        log.LogInformation(
            "Renomeados {N} grupos de '{From}' para '{To}'.",
            groups.Count, fromName, toName);
        break;
    }
    default:
        Console.WriteLine("""
            AOB.Migrator - comandos disponiveis

            Corrente (uso em producao):
              db-update                       - aplica migrations EF Core pendentes
                                                (chamado automaticamente por deploy.py)
              ping                            - health check Postgres (+ Joomla se
                                                configurado)
              nom-rename-group <de> <para>    - renomeia display name de grupos de
                                                nomenclatura

            Bootstrap-only (ja corrido; NAO usar em prod corrente):
              seed                            - insere sites + user admin base
              seed-socios                     - cria socios de teste
              seed-nomenclature-2026          - nomenclatura BVA INT 2026
              home-content-seed               - Site.HomeConfig.mission das categorias
                                                'quem somos'
              patch-bva-home                  - actualiza areas/CTA do site BVA
              migrate categories|articles|downloads|menus|images|all
                                              - migra dados Joomla -> Postgres
              redirects                       - gera nginx redirects.map do Joomla
              sponsors                        - migra banners Joomla -> tabela sponsors
              articles-fix-external-images    - baixa imagens externas para local
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
