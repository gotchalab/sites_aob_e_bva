using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSociosQuotasPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "socios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    NumeroSocio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NomeCompleto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NIF = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DataNascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Morada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CodigoPostal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Localidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EspeciesInteresse = table.Column<List<string>>(type: "text[]", nullable: false),
                    DataInscricao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_socios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_socios_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_socios_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedidos_anilha",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocioId = table.Column<int>(type: "integer", nullable: false),
                    EspecieCientifica = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EspecieNomeComum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Diametro = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    DataPedido = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataEntrega = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos_anilha", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pedidos_anilha_socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocioId = table.Column<int>(type: "integer", nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metodo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Recibo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotas_socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_anilha_SocioId_Estado_DataPedido",
                table: "pedidos_anilha",
                columns: new[] { "SocioId", "Estado", "DataPedido" });

            migrationBuilder.CreateIndex(
                name: "IX_quotas_SocioId_Ano",
                table: "quotas",
                columns: new[] { "SocioId", "Ano" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_socios_Email",
                table: "socios",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_socios_SiteId_NumeroSocio",
                table: "socios",
                columns: new[] { "SiteId", "NumeroSocio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_socios_UserId",
                table: "socios",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedidos_anilha");

            migrationBuilder.DropTable(
                name: "quotas");

            migrationBuilder.DropTable(
                name: "socios");
        }
    }
}
