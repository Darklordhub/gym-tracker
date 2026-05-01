using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionCatalogCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NutritionCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    FoodType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Barcode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CaloriesPer100g = table.Column<decimal>(type: "numeric", nullable: false),
                    ProteinPer100g = table.Column<decimal>(type: "numeric", nullable: false),
                    CarbsPer100g = table.Column<decimal>(type: "numeric", nullable: false),
                    FatPer100g = table.Column<decimal>(type: "numeric", nullable: false),
                    FiberPer100g = table.Column<decimal>(type: "numeric", nullable: true),
                    SugarPer100g = table.Column<decimal>(type: "numeric", nullable: true),
                    ProviderPayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    LastFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NutritionCatalogPortions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NutritionCatalogItemId = table.Column<int>(type: "integer", nullable: false),
                    UnitName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    GramWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    ProviderPortionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionCatalogPortions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NutritionCatalogPortions_NutritionCatalogItems_NutritionCat~",
                        column: x => x.NutritionCatalogItemId,
                        principalTable: "NutritionCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NutritionCatalogItems_Barcode",
                table: "NutritionCatalogItems",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionCatalogItems_Name",
                table: "NutritionCatalogItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionCatalogItems_Source_ExternalId",
                table: "NutritionCatalogItems",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NutritionCatalogPortions_NutritionCatalogItemId",
                table: "NutritionCatalogPortions",
                column: "NutritionCatalogItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NutritionCatalogPortions");

            migrationBuilder.DropTable(
                name: "NutritionCatalogItems");
        }
    }
}
