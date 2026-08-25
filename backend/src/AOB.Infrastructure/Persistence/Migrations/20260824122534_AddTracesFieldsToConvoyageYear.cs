using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTracesFieldsToConvoyageYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Campeonato",
                table: "convoyage_years",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatriculaTraces",
                table: "convoyage_years",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Campeonato",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "MatriculaTraces",
                table: "convoyage_years");
        }
    }
}
