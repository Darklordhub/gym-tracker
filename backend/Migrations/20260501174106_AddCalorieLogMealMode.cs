using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCalorieLogMealMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRolledUpAt",
                table: "UserCalorieLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMode",
                table: "UserCalorieLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCarbs",
                table: "UserCalorieLogs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalFat",
                table: "UserCalorieLogs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalProtein",
                table: "UserCalorieLogs",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRolledUpAt",
                table: "UserCalorieLogs");

            migrationBuilder.DropColumn(
                name: "SourceMode",
                table: "UserCalorieLogs");

            migrationBuilder.DropColumn(
                name: "TotalCarbs",
                table: "UserCalorieLogs");

            migrationBuilder.DropColumn(
                name: "TotalFat",
                table: "UserCalorieLogs");

            migrationBuilder.DropColumn(
                name: "TotalProtein",
                table: "UserCalorieLogs");
        }
    }
}
