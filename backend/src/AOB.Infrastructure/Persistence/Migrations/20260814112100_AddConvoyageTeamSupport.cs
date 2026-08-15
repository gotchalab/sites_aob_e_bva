using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConvoyageTeamSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EquipaId",
                table: "convoyage_bird_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosicaoEquipa",
                table: "convoyage_bird_entries",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_convoyage_bird_entries_EquipaId",
                table: "convoyage_bird_entries",
                column: "EquipaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_convoyage_bird_entries_EquipaId",
                table: "convoyage_bird_entries");

            migrationBuilder.DropColumn(
                name: "EquipaId",
                table: "convoyage_bird_entries");

            migrationBuilder.DropColumn(
                name: "PosicaoEquipa",
                table: "convoyage_bird_entries");
        }
    }
}
