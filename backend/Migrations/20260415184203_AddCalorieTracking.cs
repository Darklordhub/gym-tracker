using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCalorieTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalorieTargetMode",
                table: "GoalSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<int>(
                name: "DailyCalorieTarget",
                table: "GoalSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserCalorieLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CaloriesConsumed = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCalorieLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCalorieLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCalorieLogs_UserId_Date",
                table: "UserCalorieLogs",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCalorieLogs");

            migrationBuilder.DropColumn(
                name: "CalorieTargetMode",
                table: "GoalSettings");

            migrationBuilder.DropColumn(
                name: "DailyCalorieTarget",
                table: "GoalSettings");
        }
    }
}
