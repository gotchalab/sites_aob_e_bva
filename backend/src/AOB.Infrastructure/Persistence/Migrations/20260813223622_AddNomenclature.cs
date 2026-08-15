using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AOB.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNomenclature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nomenclature_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConvoyageYearId = table.Column<int>(type: "integer", nullable: false),
                    Species = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    CodePrefix = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nomenclature_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nomenclature_groups_convoyage_years_ConvoyageYearId",
                        column: x => x.ConvoyageYearId,
                        principalTable: "convoyage_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nomenclature_classes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomenclatureGroupId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Mutation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nomenclature_classes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nomenclature_classes_nomenclature_groups_NomenclatureGroupId",
                        column: x => x.NomenclatureGroupId,
                        principalTable: "nomenclature_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "convoyage_bird_entries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FormSubmissionId = table.Column<int>(type: "integer", nullable: false),
                    BirdOrder = table.Column<int>(type: "integer", nullable: false),
                    NomenclatureClassId = table.Column<int>(type: "integer", nullable: false),
                    RingNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_convoyage_bird_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_convoyage_bird_entries_form_submissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "form_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_convoyage_bird_entries_nomenclature_classes_NomenclatureCla~",
                        column: x => x.NomenclatureClassId,
                        principalTable: "nomenclature_classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_convoyage_bird_entries_FormSubmissionId_BirdOrder",
                table: "convoyage_bird_entries",
                columns: new[] { "FormSubmissionId", "BirdOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_convoyage_bird_entries_NomenclatureClassId",
                table: "convoyage_bird_entries",
                column: "NomenclatureClassId");

            migrationBuilder.CreateIndex(
                name: "IX_nomenclature_classes_NomenclatureGroupId_Code_Mutation",
                table: "nomenclature_classes",
                columns: new[] { "NomenclatureGroupId", "Code", "Mutation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nomenclature_classes_NomenclatureGroupId_SortOrder",
                table: "nomenclature_classes",
                columns: new[] { "NomenclatureGroupId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_nomenclature_groups_ConvoyageYearId_CodePrefix",
                table: "nomenclature_groups",
                columns: new[] { "ConvoyageYearId", "CodePrefix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nomenclature_groups_ConvoyageYearId_Species_EntryType",
                table: "nomenclature_groups",
                columns: new[] { "ConvoyageYearId", "Species", "EntryType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "convoyage_bird_entries");

            migrationBuilder.DropTable(
                name: "nomenclature_classes");

            migrationBuilder.DropTable(
                name: "nomenclature_groups");
        }
    }
}
