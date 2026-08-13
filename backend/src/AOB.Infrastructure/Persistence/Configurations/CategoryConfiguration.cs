using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.HasIndex(x => new { x.SiteId, x.Slug }).IsUnique();
        b.HasIndex(x => x.LegacyId);

        b.HasOne(x => x.Site)
            .WithMany(s => s.Categories)
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Parent)
            .WithMany(p => p!.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
