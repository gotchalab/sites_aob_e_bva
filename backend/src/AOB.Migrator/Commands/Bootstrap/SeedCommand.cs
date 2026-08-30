using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands.Bootstrap;

public class SeedCommand(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    ILogger<SeedCommand> log)
{
    private const string DefaultAdminEmail = "admin@aobarcelos.pt";
    private const string DefaultAdminPassword = "ChangeMe!Admin2026";

    private const string AobHomeConfig = """
        {
          "missionTitle": "A tua referência para criar aves com confiança em Barcelos.",
          "missionQuote": "Na AOB tens anilhas federadas, exposições reconhecidas e uma comunidade activa — tudo o que precisas para criar aves com suporte em Barcelos.",
          "mission": "A Associação Ornitológica de Barcelos nasceu da vontade de criar um espaço de referência para os criadores de aves da região. Ao longo de décadas, construímos uma comunidade assente na partilha de conhecimento, na criação responsável e no amor pelas aves.\n\nComo sócio tens acesso a anilhas federadas com o teu código único de criador, podes inscrever as tuas aves em exposições e concursos com classificação oficial, contas com apoio técnico de criadores experientes e beneficias de um portal online para gerir pedidos e quotas.",
          "ctaTitle": "Junta-te à associação",
          "ctaSubtitle": "Anilhas federadas, exposições reconhecidas, apoio técnico e um portal online — tudo numa só associação com décadas de tradição.",
          "ctaLabel": "Quero ser sócio",
          "ctaHref": "/inscricao-socio",
          "benefits": [
            {"icon": "Award",      "label": "Anilhas federadas",        "description": "Código único de criador reconhecido pela FONP — identificação permanente e rastreável para cada ave que crias."},
            {"icon": "Calendar",   "label": "Exposições e concursos",   "description": "Participa em mostras locais e nacionais com julgamento oficial e classificação reconhecida."},
            {"icon": "ShieldCheck","label": "Área reservada online",    "description": "Gere quotas, pedidos de anilhas e todos os teus dados de sócio num portal exclusivo, a qualquer hora."},
            {"icon": "Users",      "label": "Comunidade e apoio técnico","description": "Aprende com criadores experientes, partilha conhecimento e recebe apoio técnico quando mais precisas."}
          ]
        }
        """;

    private const string BvaHomeConfig = """
        {
          "missionTitle": "A associação técnica portuguesa de Agapornis.",
          "missionQuote": "Décadas dedicadas à criação técnica de Agapornis — promovendo standards, exposições e a partilha entre criadores em Portugal.",
          "mission": "Somos a associação técnica portuguesa de Agapornis, dedicada à criação responsável, ao julgamento por standards internacionais e à divulgação técnica destas aves. Ao longo de décadas construímos uma comunidade que junta tradição, técnica e partilha de conhecimento.\n\nEmitimos anilhas oficiais para os nossos sócios, organizamos as edições BVA Masters, participamos em concursos internacionais e mantemos uma rede activa de comunicação entre criadores portugueses e europeus.",
          "ctaTitle": "Junta-te à associação",
          "ctaSubtitle": "Faz parte da associação técnica portuguesa de Agapornis e liga-te à comunidade nacional de criadores.",
          "ctaLabel": "Quero ser sócio",
          "ctaHref": "/inscricao-socio",
          "benefits": [
            {"icon": "Award",      "label": "Standards oficiais",        "description": "Aceder aos padrões técnicos reconhecidos internacionalmente."},
            {"icon": "Calendar",   "label": "Acesso a exposições",       "description": "Participar nas edições BVA e nos concursos parceiros."},
            {"icon": "ShieldCheck","label": "Área reservada online",     "description": "Gerir quotas, dados e pedidos de anilhas num só sítio."},
            {"icon": "Users",      "label": "Comunidade técnica",        "description": "Ligação directa a criadores e juízes de Agapornis."},
            {"icon": "Plane",      "label": "Convoyage à BVA Masters",   "description": "Leva as tuas aves à maior exposição temática de Agapornis da Europa, com a delegação portuguesa."}
          ]
        }
        """;

    public async Task RunAsync()
    {
        log.LogInformation("=== Seed ===");

        foreach (var role in new[] { AppRoles.Admin, AppRoles.Editor, AppRoles.Socio })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
                log.LogInformation("Role criada: {Role}", role);
            }
        }

        var aob = await UpsertSite(new Site
        {
            Slug = "aob",
            Name = "Associação Ornitológica de Barcelos",
            Domain = "aobarcelos.pt",
            Description = "Clube ornitológico de Barcelos — criação responsável, exposições de aves e divulgação da ornitologia.",
            PrimaryColor = "#1e6091",
            ContactEmail = "aobarcelos@gmail.com",
            HomeConfig = AobHomeConfig,
        });

        var bva = await UpsertSite(new Site
        {
            Slug = "bva",
            Name = "BVA Portugal",
            Domain = "bva-p.aobarcelos.pt",
            Description = "Filiação portuguesa da BVA International (Belgische Vereniging Agaporniden). Clube técnico dedicado ao estudo, standards, exposições e divulgação dos agapornis.",
            Tagline = "Filiação portuguesa da BVA International — Belgische Vereniging Agaporniden, fundada na Bélgica em 1992.",
            PrimaryColor = "#0f766e",
            ContactEmail = "bvap.aob@gmail.com",
            HomeConfig = BvaHomeConfig,
        });

        var admin = await users.FindByEmailAsync(DefaultAdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = DefaultAdminEmail,
                Email = DefaultAdminEmail,
                EmailConfirmed = true,
                FullName = "Administrador",
                PreferredSiteId = aob.Id,
            };
            var res = await users.CreateAsync(admin, DefaultAdminPassword);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors)
                    log.LogError("Falha a criar admin: {Code} {Desc}", e.Code, e.Description);
                return;
            }
            await users.AddToRoleAsync(admin, AppRoles.Admin);
            log.LogWarning("Admin criado: {Email} (password: {Password}) — MUDAR APOS 1o LOGIN",
                DefaultAdminEmail, DefaultAdminPassword);
        }
        else
        {
            log.LogInformation("Admin ja existe: {Email}", admin.Email);
        }

        log.LogInformation("=== Seed OK ===");
    }

    private async Task<Site> UpsertSite(Site incoming)
    {
        var existing = await db.Sites.FirstOrDefaultAsync(s => s.Slug == incoming.Slug);
        if (existing is null)
        {
            db.Sites.Add(incoming);
            await db.SaveChangesAsync();
            log.LogInformation("Site criado: {Slug} -> {Domain} (Id={Id})", incoming.Slug, incoming.Domain, incoming.Id);
            return incoming;
        }
        existing.Name = incoming.Name;
        existing.Domain = incoming.Domain;
        existing.Description = incoming.Description;
        existing.PrimaryColor = incoming.PrimaryColor;
        existing.ContactEmail = incoming.ContactEmail;
        if (!string.IsNullOrWhiteSpace(incoming.Tagline))
            existing.Tagline = incoming.Tagline;
        if (string.IsNullOrWhiteSpace(existing.HomeConfig) && !string.IsNullOrWhiteSpace(incoming.HomeConfig))
            existing.HomeConfig = incoming.HomeConfig;
        await db.SaveChangesAsync();
        log.LogInformation("Site atualizado: {Slug} (Id={Id})", existing.Slug, existing.Id);
        return existing;
    }
}
