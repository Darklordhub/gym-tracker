using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveWorkoutSessionExerciseSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActiveWorkoutSessionExerciseEntryId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Reps = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveWorkoutSessionExerciseSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveWorkoutSessionExerciseSets_ActiveWorkoutSessionExerci~",
                        column: x => x.ActiveWorkoutSessionExerciseEntryId,
                        principalTable: "ActiveWorkoutSessionExerciseEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExerciseEntryId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Reps = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseSets_ExerciseEntries_ExerciseEntryId",
                        column: x => x.ExerciseEntryId,
                        principalTable: "ExerciseEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutTemplateExerciseSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkoutTemplateExerciseEntryId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Reps = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplateExerciseSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutTemplateExerciseSets_WorkoutTemplateExerciseEntries_~",
                        column: x => x.WorkoutTemplateExerciseEntryId,
                        principalTable: "WorkoutTemplateExerciseEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveWorkoutSessionExerciseSets_ActiveWorkoutSessionExerci~",
                table: "ActiveWorkoutSessionExerciseSets",
                column: "ActiveWorkoutSessionExerciseEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseSets_ExerciseEntryId",
                table: "ExerciseSets",
                column: "ExerciseEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateExerciseSets_WorkoutTemplateExerciseEntryId",
                table: "WorkoutTemplateExerciseSets",
                column: "WorkoutTemplateExerciseEntryId");

            migrationBuilder.Sql("""
                INSERT INTO "ExerciseSets" ("ExerciseEntryId", "Order", "Reps", "WeightKg")
                SELECT "Id", series_index, "Reps", "WeightKg"
                FROM "ExerciseEntries",
                     generate_series(1, GREATEST("Sets", 1)) AS series_index;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "WorkoutTemplateExerciseSets" ("WorkoutTemplateExerciseEntryId", "Order", "Reps", "WeightKg")
                SELECT "Id", series_index, "Reps", "WeightKg"
                FROM "WorkoutTemplateExerciseEntries",
                     generate_series(1, GREATEST("Sets", 1)) AS series_index;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "ActiveWorkoutSessionExerciseSets" ("ActiveWorkoutSessionExerciseEntryId", "Order", "Reps", "WeightKg")
                SELECT "Id", series_index, "Reps", "WeightKg"
                FROM "ActiveWorkoutSessionExerciseEntries",
                     generate_series(1, GREATEST("Sets", 1)) AS series_index;
                """);

            migrationBuilder.DropColumn(
                name: "Reps",
                table: "WorkoutTemplateExerciseEntries");

            migrationBuilder.DropColumn(
                name: "Sets",
                table: "WorkoutTemplateExerciseEntries");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "WorkoutTemplateExerciseEntries");

            migrationBuilder.DropColumn(
                name: "Reps",
                table: "ExerciseEntries");

            migrationBuilder.DropColumn(
                name: "Sets",
                table: "ExerciseEntries");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "ExerciseEntries");

            migrationBuilder.DropColumn(
                name: "Reps",
                table: "ActiveWorkoutSessionExerciseEntries");

            migrationBuilder.DropColumn(
                name: "Sets",
                table: "ActiveWorkoutSessionExerciseEntries");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "ActiveWorkoutSessionExerciseEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Reps",
                table: "WorkoutTemplateExerciseEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sets",
                table: "WorkoutTemplateExerciseEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "WorkoutTemplateExerciseEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Reps",
                table: "ExerciseEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sets",
                table: "ExerciseEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "ExerciseEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Reps",
                table: "ActiveWorkoutSessionExerciseEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sets",
                table: "ActiveWorkoutSessionExerciseEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "ActiveWorkoutSessionExerciseEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE "ExerciseEntries" entry
                SET "Sets" = source."SetCount",
                    "Reps" = source."Reps",
                    "WeightKg" = source."WeightKg"
                FROM (
                    SELECT "ExerciseEntryId",
                           COUNT(*)::integer AS "SetCount",
                           MIN("Reps") AS "Reps",
                           MIN("WeightKg") AS "WeightKg"
                    FROM "ExerciseSets"
                    GROUP BY "ExerciseEntryId"
                ) source
                WHERE entry."Id" = source."ExerciseEntryId";
                """);

            migrationBuilder.Sql("""
                UPDATE "WorkoutTemplateExerciseEntries" entry
                SET "Sets" = source."SetCount",
                    "Reps" = source."Reps",
                    "WeightKg" = source."WeightKg"
                FROM (
                    SELECT "WorkoutTemplateExerciseEntryId",
                           COUNT(*)::integer AS "SetCount",
                           MIN("Reps") AS "Reps",
                           MIN("WeightKg") AS "WeightKg"
                    FROM "WorkoutTemplateExerciseSets"
                    GROUP BY "WorkoutTemplateExerciseEntryId"
                ) source
                WHERE entry."Id" = source."WorkoutTemplateExerciseEntryId";
                """);

            migrationBuilder.Sql("""
                UPDATE "ActiveWorkoutSessionExerciseEntries" entry
                SET "Sets" = source."SetCount",
                    "Reps" = source."Reps",
                    "WeightKg" = source."WeightKg"
                FROM (
                    SELECT "ActiveWorkoutSessionExerciseEntryId",
                           COUNT(*)::integer AS "SetCount",
                           MIN("Reps") AS "Reps",
                           MIN("WeightKg") AS "WeightKg"
                    FROM "ActiveWorkoutSessionExerciseSets"
                    GROUP BY "ActiveWorkoutSessionExerciseEntryId"
                ) source
                WHERE entry."Id" = source."ActiveWorkoutSessionExerciseEntryId";
                """);

            migrationBuilder.DropTable(
                name: "ActiveWorkoutSessionExerciseSets");

            migrationBuilder.DropTable(
                name: "ExerciseSets");

            migrationBuilder.DropTable(
                name: "WorkoutTemplateExerciseSets");
        }
    }
}
