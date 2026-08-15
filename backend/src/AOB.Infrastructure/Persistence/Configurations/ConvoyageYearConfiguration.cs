using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class ConvoyageYearConfiguration : IEntityTypeConfiguration<ConvoyageYear>
{
    public void Configure(EntityTypeBuilder<ConvoyageYear> b)
    {
        b.ToTable("convoyage_years");
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => new { x.SiteId, x.Year }).IsUnique();
        b.HasIndex(x => new { x.SiteId, x.IsActive });

        b.Property(x => x.RegistrationClosesAt);

        b.Property(x => x.NumCargasAlvo).HasDefaultValue(23);
        b.Property(x => x.CapacidadePorCarga).HasDefaultValue(20);
        b.Property(x => x.MinPorCarga).HasDefaultValue(16);
        b.Property(x => x.TransportadorasJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        b.HasOne(x => x.Site)
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransportCargaConfiguration : IEntityTypeConfiguration<TransportCarga>
{
    public void Configure(EntityTypeBuilder<TransportCarga> b)
    {
        b.ToTable("transport_cargas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
        b.Property(x => x.TransportadoraNome).HasMaxLength(200).IsRequired();
        b.Property(x => x.ZonasLabel).HasMaxLength(300).IsRequired();
        b.Property(x => x.Notas).HasMaxLength(500);
        b.HasIndex(x => new { x.ConvoyageYearId, x.SortOrder });
        b.HasIndex(x => new { x.ConvoyageYearId, x.Codigo }).IsUnique();

        b.HasOne(x => x.ConvoyageYear)
            .WithMany(y => y.TransportCargas)
            .HasForeignKey(x => x.ConvoyageYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransportCargaSubmissionConfiguration : IEntityTypeConfiguration<TransportCargaSubmission>
{
    public void Configure(EntityTypeBuilder<TransportCargaSubmission> b)
    {
        b.ToTable("transport_carga_submissions");
        b.HasKey(x => x.Id);
        // Uma submissão pode aparecer em várias cargas se o criador tiver mais
        // aves do que a capacidade (as aves de venda e concurso podem ir em
        // cargas separadas). Cada par (carga, submissão) continua único.
        b.HasIndex(x => new { x.TransportCargaId, x.FormSubmissionId }).IsUnique();
        b.HasIndex(x => x.FormSubmissionId);

        b.HasOne(x => x.TransportCarga)
            .WithMany(c => c.Submissoes)
            .HasForeignKey(x => x.TransportCargaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.FormSubmission)
            .WithMany()
            .HasForeignKey(x => x.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConvoyageCollectionPointConfiguration : IEntityTypeConfiguration<ConvoyageCollectionPoint>
{
    public void Configure(EntityTypeBuilder<ConvoyageCollectionPoint> b)
    {
        b.ToTable("convoyage_collection_points");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Location).HasMaxLength(200);

        b.HasOne(x => x.ConvoyageYear)
            .WithMany(y => y.CollectionPoints)
            .HasForeignKey(x => x.ConvoyageYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
