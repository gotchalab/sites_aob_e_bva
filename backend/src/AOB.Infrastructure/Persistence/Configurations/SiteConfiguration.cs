using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> b)
    {
        b.ToTable("sites");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Domain).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Domain).IsUnique();
        b.Property(x => x.PrimaryColor).HasMaxLength(20);
        b.Property(x => x.ContactEmail).HasMaxLength(200);
        b.Property(x => x.Tagline).HasMaxLength(300);
        b.Property(x => x.HomeConfig).HasColumnType("jsonb");
        b.Property(x => x.Announcement).HasColumnType("jsonb");
    }
}
