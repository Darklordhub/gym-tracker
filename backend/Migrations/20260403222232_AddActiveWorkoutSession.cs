using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveWorkoutSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveWorkoutSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveWorkoutSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActiveWorkoutSessionExerciseEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActiveWorkoutSessionId = table.Column<int>(type: "integer", nullable: false),
                    ExerciseName = table.Column<string>(type: "text", nullable: false),
                    Sets = table.Column<int>(type: "integer", nullable: false),
                    Reps = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveWorkoutSessionExerciseEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveWorkoutSessionExerciseEntries_ActiveWorkoutSessions_A~",
                        column: x => x.ActiveWorkoutSessionId,
                        principalTable: "ActiveWorkoutSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveWorkoutSessionExerciseEntries_ActiveWorkoutSessionId",
                table: "ActiveWorkoutSessionExerciseEntries",
                column: "ActiveWorkoutSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveWorkoutSessionExerciseEntries");

            migrationBuilder.DropTable(
                name: "ActiveWorkoutSessions");
        }
    }
}
