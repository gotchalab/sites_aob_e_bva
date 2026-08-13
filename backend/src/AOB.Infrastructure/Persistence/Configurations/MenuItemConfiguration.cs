using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> b)
    {
        b.ToTable("menu_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.MenuType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Url).HasMaxLength(500);

        b.HasIndex(x => new { x.SiteId, x.MenuType, x.SortOrder });

        b.HasOne(x => x.Site)
            .WithMany(s => s.MenuItems)
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Parent)
            .WithMany(p => p!.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
