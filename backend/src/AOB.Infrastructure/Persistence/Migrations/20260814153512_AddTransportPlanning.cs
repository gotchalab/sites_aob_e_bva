using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapacidadePorCarga",
                table: "convoyage_years",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "MinPorCarga",
                table: "convoyage_years",
                type: "integer",
                nullable: false,
                defaultValue: 16);

            migrationBuilder.AddColumn<int>(
                name: "NumCargasAlvo",
                table: "convoyage_years",
                type: "integer",
                nullable: false,
                defaultValue: 23);

            migrationBuilder.AddColumn<string>(
                name: "TransportadorasJson",
                table: "convoyage_years",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "transport_cargas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConvoyageYearId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TransportadoraNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ZonasLabel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transport_cargas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transport_cargas_convoyage_years_ConvoyageYearId",
                        column: x => x.ConvoyageYearId,
                        principalTable: "convoyage_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transport_carga_submissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransportCargaId = table.Column<int>(type: "integer", nullable: false),
                    FormSubmissionId = table.Column<int>(type: "integer", nullable: false),
                    NumAvesConcurso = table.Column<int>(type: "integer", nullable: false),
                    NumAvesVenda = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transport_carga_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transport_carga_submissions_form_submissions_FormSubmission~",
                        column: x => x.FormSubmissionId,
                        principalTable: "form_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transport_carga_submissions_transport_cargas_TransportCarga~",
                        column: x => x.TransportCargaId,
                        principalTable: "transport_cargas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transport_carga_submissions_FormSubmissionId",
                table: "transport_carga_submissions",
                column: "FormSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_carga_submissions_TransportCargaId_FormSubmission~",
                table: "transport_carga_submissions",
                columns: new[] { "TransportCargaId", "FormSubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_cargas_ConvoyageYearId_Codigo",
                table: "transport_cargas",
                columns: new[] { "ConvoyageYearId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_cargas_ConvoyageYearId_SortOrder",
                table: "transport_cargas",
                columns: new[] { "ConvoyageYearId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transport_carga_submissions");

            migrationBuilder.DropTable(
                name: "transport_cargas");

            migrationBuilder.DropColumn(
                name: "CapacidadePorCarga",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "MinPorCarga",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "NumCargasAlvo",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "TransportadorasJson",
                table: "convoyage_years");
        }
    }
}
