using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> b)
    {
        b.ToTable("sponsors");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.LogoPath).HasMaxLength(500).IsRequired();
        b.Property(x => x.ClickUrl).HasMaxLength(500);
        b.Property(x => x.Tier).HasConversion<int>();
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => new { x.SiteId, x.Slug }).IsUnique();
        b.HasIndex(x => new { x.SiteId, x.IsPublished, x.Tier, x.SortOrder });
        b.HasIndex(x => x.LegacyId);

        b.HasOne(x => x.Site)
            .WithMany(s => s.Sponsors)
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
