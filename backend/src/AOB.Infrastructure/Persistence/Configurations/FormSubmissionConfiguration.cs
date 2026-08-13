using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> b)
    {
        b.ToTable("form_submissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.DataJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasIndex(x => new { x.SiteId, x.FormType, x.Status, x.SubmittedAt });

        b.HasOne(x => x.Site)
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.HandledBy)
            .WithMany()
            .HasForeignKey(x => x.HandledById)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.ConvoyageYear)
            .WithMany(cy => cy.Submissions)
            .HasForeignKey(x => x.ConvoyageYearId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
