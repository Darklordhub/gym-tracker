using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseMediaDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseMediaDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExerciseCatalogItemId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Queued"),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PromptText = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SourceSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    GeneratedThumbnailUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GeneratedVideoUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GenerationProvider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    GenerationModel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ProviderJobId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PublishedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMediaDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseMediaDrafts_ExerciseCatalogItems_ExerciseCatalogIte~",
                        column: x => x.ExerciseCatalogItemId,
                        principalTable: "ExerciseCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaDrafts_ExerciseCatalogItemId",
                table: "ExerciseMediaDrafts",
                column: "ExerciseCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaDrafts_Status",
                table: "ExerciseMediaDrafts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaDrafts_UpdatedAt",
                table: "ExerciseMediaDrafts",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseMediaDrafts");
        }
    }
}
