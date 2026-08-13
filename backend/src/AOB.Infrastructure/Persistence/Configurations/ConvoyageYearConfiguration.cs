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

        b.HasOne(x => x.Site)
            .WithMany()
            .HasForeignKey(x => x.SiteId)
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
