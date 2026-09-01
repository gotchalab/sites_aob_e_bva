using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportSubmissionDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumAvesTransporteBePt",
                table: "transport_carga_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumAvesTransportePtBe",
                table: "transport_carga_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumAvesTransporteBePt",
                table: "transport_carga_submissions");

            migrationBuilder.DropColumn(
                name: "NumAvesTransportePtBe",
                table: "transport_carga_submissions");
        }
    }
}
