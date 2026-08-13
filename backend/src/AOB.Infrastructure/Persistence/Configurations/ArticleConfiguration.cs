using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> b)
    {
        b.ToTable("articles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(300).IsRequired();
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.Excerpt).HasMaxLength(1000);
        b.Property(x => x.MetaTitle).HasMaxLength(200);
        b.Property(x => x.MetaDescription).HasMaxLength(500);
        b.Property(x => x.CoverImagePath).HasMaxLength(500);

        b.HasIndex(x => new { x.SiteId, x.Slug }).IsUnique();
        b.HasIndex(x => x.LegacyId);
        b.HasIndex(x => new { x.SiteId, x.IsPublished, x.PublishedAt });
        b.HasIndex(x => new { x.SiteId, x.IsFeatured, x.PublishedAt });

        b.HasOne(x => x.Site)
            .WithMany(s => s.Articles)
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Category)
            .WithMany(c => c.Articles)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.Tags)
            .HasColumnType("text[]");
    }
}
