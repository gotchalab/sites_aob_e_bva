using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConvoyageYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConvoyageYearId",
                table: "form_submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "convoyage_years",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_convoyage_years", x => x.Id);
                    table.ForeignKey(
                        name: "FK_convoyage_years_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "convoyage_collection_points",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConvoyageYearId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_convoyage_collection_points", x => x.Id);
                    table.ForeignKey(
                        name: "FK_convoyage_collection_points_convoyage_years_ConvoyageYearId",
                        column: x => x.ConvoyageYearId,
                        principalTable: "convoyage_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_form_submissions_ConvoyageYearId",
                table: "form_submissions",
                column: "ConvoyageYearId");

            migrationBuilder.CreateIndex(
                name: "IX_convoyage_collection_points_ConvoyageYearId",
                table: "convoyage_collection_points",
                column: "ConvoyageYearId");

            migrationBuilder.CreateIndex(
                name: "IX_convoyage_years_SiteId_IsActive",
                table: "convoyage_years",
                columns: new[] { "SiteId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_convoyage_years_SiteId_Year",
                table: "convoyage_years",
                columns: new[] { "SiteId", "Year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_form_submissions_convoyage_years_ConvoyageYearId",
                table: "form_submissions",
                column: "ConvoyageYearId",
                principalTable: "convoyage_years",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_form_submissions_convoyage_years_ConvoyageYearId",
                table: "form_submissions");

            migrationBuilder.DropTable(
                name: "convoyage_collection_points");

            migrationBuilder.DropTable(
                name: "convoyage_years");

            migrationBuilder.DropIndex(
                name: "IX_form_submissions_ConvoyageYearId",
                table: "form_submissions");

            migrationBuilder.DropColumn(
                name: "ConvoyageYearId",
                table: "form_submissions");
        }
    }
}
