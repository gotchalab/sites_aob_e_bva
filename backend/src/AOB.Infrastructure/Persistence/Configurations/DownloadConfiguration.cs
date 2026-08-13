using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class DownloadConfiguration : IEntityTypeConfiguration<Download>
{
    public void Configure(EntityTypeBuilder<Download> b)
    {
        b.ToTable("downloads");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(300).IsRequired();
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        b.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(100);

        b.HasIndex(x => new { x.SiteId, x.Slug }).IsUnique();
        b.HasIndex(x => x.LegacyId);

        b.HasOne(x => x.Site)
            .WithMany(s => s.Downloads)
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Category)
            .WithMany(c => c!.Downloads)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
