using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands;

public class SeedSociosCommand(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    ILogger<SeedSociosCommand> log)
{
    private const string TestPassword = "TesteSocio123!";

    public async Task RunAsync()
    {
        log.LogInformation("=== Seed sócios de teste ===");

        var aob = await db.Sites.FirstAsync(s => s.Slug == "aob");
        var bva = await db.Sites.FirstAsync(s => s.Slug == "bva");

        var socioAob = await EnsureSocio(aob, "S9999", "Teste Socio AOB", "teste.socio@example.pt");
        var socioBva = await EnsureSocio(bva, "B9999", "Teste Socio BVA", "teste.socio.bva@example.pt");

        await EnsureUserForSocio(socioAob);
        await EnsureUserForSocio(socioBva);

        await EnsureQuotas(socioAob);
        await EnsureQuotas(socioBva);

        await db.SaveChangesAsync();

        log.LogWarning("Seed OK. Login sócio AOB: {Email} / {Pass}", socioAob.Email, TestPassword);
        log.LogWarning("Seed OK. Login sócio BVA: {Email} / {Pass}", socioBva.Email, TestPassword);
    }

    private async Task<Socio> EnsureSocio(Site site, string numero, string nome, string email)
    {
        var existing = await db.Socios.FirstOrDefaultAsync(s => s.SiteId == site.Id && s.NumeroSocio == numero);
        if (existing is not null)
        {
            log.LogInformation("Sócio já existe: {Site}/{Numero}", site.Slug, numero);
            return existing;
        }
        var s = new Socio
        {
            SiteId = site.Id,
            NumeroSocio = numero,
            NomeCompleto = nome,
            Email = email,
            Telefone = "912345678",
            NIF = "123456789",
            Morada = "Rua de Teste, 1",
            CodigoPostal = "4750-000",
            Localidade = "Barcelos",
            EspeciesInteresse = new List<string> { "Agapornis roseicollis" },
            DataInscricao = DateTime.UtcNow.AddYears(-2),
            Estado = SocioEstado.Ativo,
        };
        db.Socios.Add(s);
        await db.SaveChangesAsync();
        log.LogInformation("Sócio criado: {Site}/{Numero}", site.Slug, numero);
        return s;
    }

    private async Task EnsureUserForSocio(Socio socio)
    {
        if (socio.UserId is not null)
        {
            log.LogInformation("Sócio {Num} já tem user", socio.NumeroSocio);
            return;
        }
        var existing = await users.FindByEmailAsync(socio.Email);
        if (existing is not null)
        {
            socio.UserId = existing.Id;
            log.LogInformation("Sócio {Num} ligado a user existente {Email}", socio.NumeroSocio, socio.Email);
            return;
        }
        var user = new ApplicationUser
        {
            UserName = socio.Email,
            Email = socio.Email,
            EmailConfirmed = true,
            FullName = socio.NomeCompleto,
            SocioId = socio.Id,
        };
        var res = await users.CreateAsync(user, TestPassword);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors)
                log.LogError("Falha a criar user {Email}: {Code} {Desc}", socio.Email, e.Code, e.Description);
            return;
        }
        await users.AddToRoleAsync(user, AppRoles.Socio);
        socio.UserId = user.Id;
        log.LogInformation("User criado: {Email}", socio.Email);
    }

    private async Task EnsureQuotas(Socio socio)
    {
        var current = DateTime.UtcNow.Year;
        foreach (var (ano, valor, pago) in new[]
        {
            (current - 2, 30m, true),
            (current - 1, 30m, true),
            (current, 30m, false),
        })
        {
            var exists = await db.Quotas.AnyAsync(q => q.SocioId == socio.Id && q.Ano == ano);
            if (exists) continue;
            db.Quotas.Add(new Quota
            {
                SocioId = socio.Id,
                Ano = ano,
                Valor = valor,
                DataPagamento = pago ? new DateTime(ano, 3, 15, 0, 0, 0, DateTimeKind.Utc) : null,
                Metodo = pago ? "MBWay" : null,
                Recibo = pago ? $"REC-{ano}-{socio.NumeroSocio}" : null,
            });
        }
    }
}
