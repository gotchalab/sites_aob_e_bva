using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class NomenclatureGroupConfiguration : IEntityTypeConfiguration<NomenclatureGroup>
{
    public void Configure(EntityTypeBuilder<NomenclatureGroup> b)
    {
        b.ToTable("nomenclature_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.CodePrefix).HasMaxLength(3).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        b.Property(x => x.Species).HasConversion<int>();
        b.Property(x => x.EntryType).HasConversion<int>();

        b.HasIndex(x => new { x.ConvoyageYearId, x.CodePrefix }).IsUnique();
        b.HasIndex(x => new { x.ConvoyageYearId, x.Species, x.EntryType });

        b.HasOne(x => x.ConvoyageYear)
            .WithMany(y => y.NomenclatureGroups)
            .HasForeignKey(x => x.ConvoyageYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NomenclatureClassConfiguration : IEntityTypeConfiguration<NomenclatureClass>
{
    public void Configure(EntityTypeBuilder<NomenclatureClass> b)
    {
        b.ToTable("nomenclature_classes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.Property(x => x.Mutation).HasMaxLength(500).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(200);

        b.HasIndex(x => new { x.NomenclatureGroupId, x.SortOrder });
        b.HasIndex(x => new { x.NomenclatureGroupId, x.Code, x.Mutation }).IsUnique();

        b.HasOne(x => x.NomenclatureGroup)
            .WithMany(g => g.Classes)
            .HasForeignKey(x => x.NomenclatureGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConvoyageBirdEntryConfiguration : IEntityTypeConfiguration<ConvoyageBirdEntry>
{
    public void Configure(EntityTypeBuilder<ConvoyageBirdEntry> b)
    {
        b.ToTable("convoyage_bird_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.RingNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.PosicaoEquipa).HasMaxLength(1);

        b.HasIndex(x => new { x.FormSubmissionId, x.BirdOrder }).IsUnique();
        b.HasIndex(x => x.NomenclatureClassId);
        b.HasIndex(x => x.EquipaId);

        b.HasOne(x => x.FormSubmission)
            .WithMany(s => s.BirdEntries)
            .HasForeignKey(x => x.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // A class in use by any bird entry cannot be deleted — this preserves
        // the referential integrity of historical submissions.
        b.HasOne(x => x.NomenclatureClass)
            .WithMany(c => c.BirdEntries)
            .HasForeignKey(x => x.NomenclatureClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
