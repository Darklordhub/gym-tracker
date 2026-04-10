using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'Users'
                          AND column_name = 'Role'
                    ) THEN
                        ALTER TABLE "Users" ADD COLUMN "Role" text;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "Role" = 'User'
                WHERE "Role" IS NULL OR BTRIM("Role") = '';
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Users" ALTER COLUMN "Role" SET DEFAULT 'User';
                ALTER TABLE "Users" ALTER COLUMN "Role" SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "Users" DROP COLUMN IF EXISTS "Role";""");
        }
    }
}
