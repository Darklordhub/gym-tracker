using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(6000)", maxLength: 6000, nullable: true),
                    PrimaryMuscle = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SecondaryMuscles = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Equipment = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LocalMediaPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseCatalogItems_Slug",
                table: "ExerciseCatalogItems",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseCatalogItems");
        }
    }
}
