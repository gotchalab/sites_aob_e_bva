using AOB.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AOB.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Download> Downloads => Set<Download>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();
    public DbSet<Socio> Socios => Set<Socio>();
    public DbSet<Quota> Quotas => Set<Quota>();
    public DbSet<PedidoAnilha> PedidosAnilha => Set<PedidoAnilha>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<ConvoyageYear> ConvoyageYears => Set<ConvoyageYear>();
    public DbSet<ConvoyageCollectionPoint> ConvoyageCollectionPoints => Set<ConvoyageCollectionPoint>();
    public DbSet<NomenclatureGroup> NomenclatureGroups => Set<NomenclatureGroup>();
    public DbSet<NomenclatureClass> NomenclatureClasses => Set<NomenclatureClass>();
    public DbSet<ConvoyageBirdEntry> ConvoyageBirdEntries => Set<ConvoyageBirdEntry>();
    public DbSet<TransportCarga> TransportCargas => Set<TransportCarga>();
    public DbSet<TransportCargaSubmission> TransportCargaSubmissions => Set<TransportCargaSubmission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
