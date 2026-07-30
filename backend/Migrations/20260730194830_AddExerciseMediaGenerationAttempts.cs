using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseMediaGenerationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseMediaGenerationAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExerciseMediaDraftId = table.Column<int>(type: "integer", nullable: false),
                    ExerciseCatalogItemId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderJobId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMediaGenerationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseMediaGenerationAttempts_ExerciseMediaDrafts_Exercis~",
                        column: x => x.ExerciseMediaDraftId,
                        principalTable: "ExerciseMediaDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaGenerationAttempts_CreatedAt",
                table: "ExerciseMediaGenerationAttempts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaGenerationAttempts_ExerciseCatalogItemId_Creat~",
                table: "ExerciseMediaGenerationAttempts",
                columns: new[] { "ExerciseCatalogItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMediaGenerationAttempts_ExerciseMediaDraftId_Create~",
                table: "ExerciseMediaGenerationAttempts",
                columns: new[] { "ExerciseMediaDraftId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseMediaGenerationAttempts");
        }
    }
}
