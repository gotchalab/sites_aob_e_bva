using AOB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AOB.Infrastructure.Persistence.Configurations;

public class SocioConfiguration : IEntityTypeConfiguration<Socio>
{
    public void Configure(EntityTypeBuilder<Socio> b)
    {
        b.ToTable("socios");
        b.HasKey(x => x.Id);
        b.Property(x => x.NumeroSocio).HasMaxLength(20).IsRequired();
        b.Property(x => x.NomeCompleto).HasMaxLength(200).IsRequired();
        b.Property(x => x.NIF).HasMaxLength(20);
        b.Property(x => x.Morada).HasMaxLength(300);
        b.Property(x => x.CodigoPostal).HasMaxLength(20);
        b.Property(x => x.Localidade).HasMaxLength(100);
        b.Property(x => x.Telefone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.FotoPath).HasMaxLength(500);
        b.Property(x => x.EspeciesInteresse).HasColumnType("text[]");

        b.HasIndex(x => new { x.SiteId, x.NumeroSocio }).IsUnique();
        b.HasIndex(x => x.Email);
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");

        b.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class QuotaConfiguration : IEntityTypeConfiguration<Quota>
{
    public void Configure(EntityTypeBuilder<Quota> b)
    {
        b.ToTable("quotas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Valor).HasPrecision(9, 2);
        b.Property(x => x.Metodo).HasMaxLength(50);
        b.Property(x => x.Recibo).HasMaxLength(50);
        b.Property(x => x.Notas).HasMaxLength(500);
        b.HasIndex(x => new { x.SocioId, x.Ano }).IsUnique();
        b.HasOne(x => x.Socio).WithMany(s => s.Quotas).HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PedidoAnilhaConfiguration : IEntityTypeConfiguration<PedidoAnilha>
{
    public void Configure(EntityTypeBuilder<PedidoAnilha> b)
    {
        b.ToTable("pedidos_anilha");
        b.HasKey(x => x.Id);
        b.Property(x => x.EspecieCientifica).HasMaxLength(200).IsRequired();
        b.Property(x => x.EspecieNomeComum).HasMaxLength(200);
        b.Property(x => x.Diametro).HasPrecision(4, 2);
        b.Property(x => x.Observacoes).HasMaxLength(1000);
        b.Property(x => x.Notas).HasMaxLength(1000);
        b.HasIndex(x => new { x.SocioId, x.Estado, x.DataPedido });
        b.HasOne(x => x.Socio).WithMany(s => s.Pedidos).HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Cascade);
    }
}
