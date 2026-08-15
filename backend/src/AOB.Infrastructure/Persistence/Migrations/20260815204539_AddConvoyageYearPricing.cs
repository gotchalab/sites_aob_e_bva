using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConvoyageYearPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecoAveBva",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 3.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoGaiola",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 3.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoInscricao",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 8.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "Quota",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 40.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "TarifaAdquirenteNaoSocio",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 20.50m);

            migrationBuilder.AddColumn<decimal>(
                name: "TarifaAdquirenteSocio",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 15.50m);

            migrationBuilder.AddColumn<decimal>(
                name: "TarifaTransporteNaoSocio",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 15.50m);

            migrationBuilder.AddColumn<decimal>(
                name: "TarifaTransporteSocio",
                table: "convoyage_years",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 5.50m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecoAveBva",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "PrecoGaiola",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "PrecoInscricao",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "Quota",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "TarifaAdquirenteNaoSocio",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "TarifaAdquirenteSocio",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "TarifaTransporteNaoSocio",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "TarifaTransporteSocio",
                table: "convoyage_years");
        }
    }
}
