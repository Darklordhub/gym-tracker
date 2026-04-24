using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseCatalogSourceExternalIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                          AND indexname = 'IX_ExerciseCatalogItems_Source_ExternalId'
                    ) THEN
                        CREATE UNIQUE INDEX "IX_ExerciseCatalogItems_Source_ExternalId"
                        ON "ExerciseCatalogItems" ("Source", "ExternalId");
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_ExerciseCatalogItems_Source_ExternalId";
                """);
        }
    }
}
