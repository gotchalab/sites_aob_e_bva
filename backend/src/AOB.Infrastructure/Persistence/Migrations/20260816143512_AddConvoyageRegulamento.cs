using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConvoyageRegulamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegulamentoDownloadId",
                table: "convoyage_years",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_convoyage_years_RegulamentoDownloadId",
                table: "convoyage_years",
                column: "RegulamentoDownloadId");

            migrationBuilder.AddForeignKey(
                name: "FK_convoyage_years_downloads_RegulamentoDownloadId",
                table: "convoyage_years",
                column: "RegulamentoDownloadId",
                principalTable: "downloads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_convoyage_years_downloads_RegulamentoDownloadId",
                table: "convoyage_years");

            migrationBuilder.DropIndex(
                name: "IX_convoyage_years_RegulamentoDownloadId",
                table: "convoyage_years");

            migrationBuilder.DropColumn(
                name: "RegulamentoDownloadId",
                table: "convoyage_years");
        }
    }
}
