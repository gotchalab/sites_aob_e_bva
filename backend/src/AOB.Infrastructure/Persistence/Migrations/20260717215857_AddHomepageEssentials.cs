using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomepageEssentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Announcement",
                table: "sites",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeConfig",
                table: "sites",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "sites",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "articles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "sponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LogoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClickUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    LegacyId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sponsors_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_articles_SiteId_IsFeatured_PublishedAt",
                table: "articles",
                columns: new[] { "SiteId", "IsFeatured", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sponsors_LegacyId",
                table: "sponsors",
                column: "LegacyId");

            migrationBuilder.CreateIndex(
                name: "IX_sponsors_SiteId_IsPublished_Tier_SortOrder",
                table: "sponsors",
                columns: new[] { "SiteId", "IsPublished", "Tier", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_sponsors_SiteId_Slug",
                table: "sponsors",
                columns: new[] { "SiteId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sponsors");

            migrationBuilder.DropIndex(
                name: "IX_articles_SiteId_IsFeatured_PublishedAt",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "Announcement",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "HomeConfig",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "articles");
        }
    }
}
