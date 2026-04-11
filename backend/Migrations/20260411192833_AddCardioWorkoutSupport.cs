using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCardioWorkoutSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardioActivityType",
                table: "Workouts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardioDistanceKm",
                table: "Workouts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardioDurationMinutes",
                table: "Workouts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardioIntensity",
                table: "Workouts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkoutType",
                table: "Workouts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardioActivityType",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "CardioDistanceKm",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "CardioDurationMinutes",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "CardioIntensity",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "WorkoutType",
                table: "Workouts");
        }
    }
}
