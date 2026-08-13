using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedHomeConfig : Migration
    {
        private const string AobJson = """
            {"missionTitle":"A tua referência para criar aves com confiança em Barcelos.","missionQuote":"Na AOB tens anilhas federadas, exposições reconhecidas e uma comunidade activa — tudo o que precisas para criar aves com suporte em Barcelos.","mission":"A Associação Ornitológica de Barcelos nasceu da vontade de criar um espaço de referência para os criadores de aves da região. Ao longo de décadas, construímos uma comunidade assente na partilha de conhecimento, na criação responsável e no amor pelas aves.\n\nComo sócio tens acesso a anilhas federadas com o teu código único de criador, podes inscrever as tuas aves em exposições e concursos com classificação oficial, contas com apoio técnico de criadores experientes e beneficias de um portal online para gerir pedidos e quotas.","ctaTitle":"Junta-te à associação","ctaSubtitle":"Anilhas federadas, exposições reconhecidas, apoio técnico e um portal online — tudo numa só associação com décadas de tradição.","ctaLabel":"Quero ser sócio","ctaHref":"/inscricao-socio","benefits":[{"icon":"Award","label":"Anilhas federadas","description":"Código único de criador reconhecido pela FONP — identificação permanente e rastreável para cada ave que crias."},{"icon":"Calendar","label":"Exposições e concursos","description":"Participa em mostras locais e nacionais com julgamento oficial e classificação reconhecida."},{"icon":"ShieldCheck","label":"Área reservada online","description":"Gere quotas, pedidos de anilhas e todos os teus dados de sócio num portal exclusivo, a qualquer hora."},{"icon":"Users","label":"Comunidade e apoio técnico","description":"Aprende com criadores experientes, partilha conhecimento e recebe apoio técnico quando mais precisas."}]}
            """;

        private const string BvaJson = """
            {"missionTitle":"A associação técnica portuguesa de Agapornis.","missionQuote":"Décadas dedicadas à criação técnica de Agapornis — promovendo standards, exposições e a partilha entre criadores em Portugal.","mission":"Somos a associação técnica portuguesa de Agapornis, dedicada à criação responsável, ao julgamento por standards internacionais e à divulgação técnica destas aves. Ao longo de décadas construímos uma comunidade que junta tradição, técnica e partilha de conhecimento.\n\nEmitimos anilhas oficiais para os nossos sócios, organizamos as edições BVA Masters, participamos em concursos internacionais e mantemos uma rede activa de comunicação entre criadores portugueses e europeus.","ctaTitle":"Junta-te à associação","ctaSubtitle":"Faz parte da associação técnica portuguesa de Agapornis e liga-te à comunidade nacional de criadores.","ctaLabel":"Quero ser sócio","ctaHref":"/inscricao-socio","benefits":[{"icon":"Award","label":"Standards oficiais","description":"Aceder aos padrões técnicos reconhecidos internacionalmente."},{"icon":"Calendar","label":"Acesso a exposições","description":"Participar nas edições BVA e nos concursos parceiros."},{"icon":"ShieldCheck","label":"Área reservada online","description":"Gerir quotas, dados e pedidos de anilhas num só sítio."},{"icon":"Users","label":"Comunidade técnica","description":"Ligação directa a criadores e juízes de Agapornis."},{"icon":"Plane","label":"Convoyage à BVA Masters","description":"Leva as tuas aves à maior exposição temática de Agapornis da Europa, com a delegação portuguesa."}]}
            """;

        // Só os benefits da BVA — usado no merge para forçar os valores correctos
        private const string BvaBenefitsJson = """
            {"benefits":[{"icon":"Award","label":"Standards oficiais","description":"Aceder aos padrões técnicos reconhecidos internacionalmente."},{"icon":"Calendar","label":"Acesso a exposições","description":"Participar nas edições BVA e nos concursos parceiros."},{"icon":"ShieldCheck","label":"Área reservada online","description":"Gerir quotas, dados e pedidos de anilhas num só sítio."},{"icon":"Users","label":"Comunidade técnica","description":"Ligação directa a criadores e juízes de Agapornis."},{"icon":"Plane","label":"Convoyage à BVA Masters","description":"Leva as tuas aves à maior exposição temática de Agapornis da Europa, com a delegação portuguesa."}]}
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE sites SET \"HomeConfig\" = '{AobJson.Trim()}'::jsonb WHERE \"Slug\" = 'aob';");

            migrationBuilder.Sql(
                $"UPDATE sites SET \"HomeConfig\" = '{BvaJson.Trim()}'::jsonb || '{BvaBenefitsJson.Trim()}'::jsonb WHERE \"Slug\" = 'bva';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE sites SET "HomeConfig" = NULL WHERE "Slug" IN ('aob', 'bva');
                """);
        }
    }
}
