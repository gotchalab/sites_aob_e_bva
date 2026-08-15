using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowSplitTransportSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transport_carga_submissions_FormSubmissionId",
                table: "transport_carga_submissions");

            migrationBuilder.CreateIndex(
                name: "IX_transport_carga_submissions_FormSubmissionId",
                table: "transport_carga_submissions",
                column: "FormSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transport_carga_submissions_FormSubmissionId",
                table: "transport_carga_submissions");

            migrationBuilder.CreateIndex(
                name: "IX_transport_carga_submissions_FormSubmissionId",
                table: "transport_carga_submissions",
                column: "FormSubmissionId",
                unique: true);
        }
    }
}
