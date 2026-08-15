using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormSubmissionLocalRecolha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_form_submissions_ConvoyageYearId",
                table: "form_submissions");

            migrationBuilder.AddColumn<int>(
                name: "LocalRecolhaId",
                table: "form_submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_form_submissions_ConvoyageYearId_LocalRecolhaId",
                table: "form_submissions",
                columns: new[] { "ConvoyageYearId", "LocalRecolhaId" });

            migrationBuilder.CreateIndex(
                name: "IX_form_submissions_LocalRecolhaId",
                table: "form_submissions",
                column: "LocalRecolhaId");

            migrationBuilder.AddForeignKey(
                name: "FK_form_submissions_convoyage_collection_points_LocalRecolhaId",
                table: "form_submissions",
                column: "LocalRecolhaId",
                principalTable: "convoyage_collection_points",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_form_submissions_convoyage_collection_points_LocalRecolhaId",
                table: "form_submissions");

            migrationBuilder.DropIndex(
                name: "IX_form_submissions_ConvoyageYearId_LocalRecolhaId",
                table: "form_submissions");

            migrationBuilder.DropIndex(
                name: "IX_form_submissions_LocalRecolhaId",
                table: "form_submissions");

            migrationBuilder.DropColumn(
                name: "LocalRecolhaId",
                table: "form_submissions");

            migrationBuilder.CreateIndex(
                name: "IX_form_submissions_ConvoyageYearId",
                table: "form_submissions",
                column: "ConvoyageYearId");
        }
    }
}
