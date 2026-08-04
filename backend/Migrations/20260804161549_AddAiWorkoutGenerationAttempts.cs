using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAiWorkoutGenerationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiWorkoutGenerationAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CandidateExerciseCount = table.Column<int>(type: "integer", nullable: false),
                    SelectedExerciseCount = table.Column<int>(type: "integer", nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SafeErrorMessage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkoutGenerationAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkoutGenerationAttempts_StartedAtUtc",
                table: "AiWorkoutGenerationAttempts",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkoutGenerationAttempts_UserId_StartedAtUtc",
                table: "AiWorkoutGenerationAttempts",
                columns: new[] { "UserId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiWorkoutGenerationAttempts");
        }
    }
}
