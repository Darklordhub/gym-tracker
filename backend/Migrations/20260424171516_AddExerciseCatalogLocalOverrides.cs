using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseCatalogLocalOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "ExerciseCatalogItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEditedAt",
                table: "ExerciseCatalogItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalInstructionsOverride",
                table: "ExerciseCatalogItems",
                type: "character varying(6000)",
                maxLength: 6000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalNameOverride",
                table: "ExerciseCatalogItems",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalThumbnailUrlOverride",
                table: "ExerciseCatalogItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalVideoUrlOverride",
                table: "ExerciseCatalogItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "ExerciseCatalogItems");

            migrationBuilder.DropColumn(
                name: "LastEditedAt",
                table: "ExerciseCatalogItems");

            migrationBuilder.DropColumn(
                name: "LocalInstructionsOverride",
                table: "ExerciseCatalogItems");

            migrationBuilder.DropColumn(
                name: "LocalNameOverride",
                table: "ExerciseCatalogItems");

            migrationBuilder.DropColumn(
                name: "LocalThumbnailUrlOverride",
                table: "ExerciseCatalogItems");

            migrationBuilder.DropColumn(
                name: "LocalVideoUrlOverride",
                table: "ExerciseCatalogItems");
        }
    }
}
