using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthAndUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "WorkoutTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Workouts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "WeightEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "GoalSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ActiveWorkoutSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplates_UserId_Name",
                table: "WorkoutTemplates",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Workouts_UserId_Date",
                table: "Workouts",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_WeightEntries_UserId_Date",
                table: "WeightEntries",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalSettings_UserId",
                table: "GoalSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActiveWorkoutSessions_UserId",
                table: "ActiveWorkoutSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ActiveWorkoutSessions_Users_UserId",
                table: "ActiveWorkoutSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GoalSettings_Users_UserId",
                table: "GoalSettings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeightEntries_Users_UserId",
                table: "WeightEntries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Workouts_Users_UserId",
                table: "Workouts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutTemplates_Users_UserId",
                table: "WorkoutTemplates",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActiveWorkoutSessions_Users_UserId",
                table: "ActiveWorkoutSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_GoalSettings_Users_UserId",
                table: "GoalSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_WeightEntries_Users_UserId",
                table: "WeightEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Workouts_Users_UserId",
                table: "Workouts");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutTemplates_Users_UserId",
                table: "WorkoutTemplates");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutTemplates_UserId_Name",
                table: "WorkoutTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Workouts_UserId_Date",
                table: "Workouts");

            migrationBuilder.DropIndex(
                name: "IX_WeightEntries_UserId_Date",
                table: "WeightEntries");

            migrationBuilder.DropIndex(
                name: "IX_GoalSettings_UserId",
                table: "GoalSettings");

            migrationBuilder.DropIndex(
                name: "IX_ActiveWorkoutSessions_UserId",
                table: "ActiveWorkoutSessions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WorkoutTemplates");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WeightEntries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "GoalSettings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ActiveWorkoutSessions");
        }
    }
}
