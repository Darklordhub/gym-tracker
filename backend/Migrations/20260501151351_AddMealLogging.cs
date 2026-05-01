using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMealLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MealType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalCalories = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalProtein = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCarbs = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalFat = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalFiber = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalSugar = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMeals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMealItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserMealId = table.Column<int>(type: "integer", nullable: false),
                    NutritionCatalogItemId = table.Column<int>(type: "integer", nullable: true),
                    FoodNameSnapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    BrandNameSnapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    SourceProvider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalFoodId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConsumedGrams = table.Column<decimal>(type: "numeric", nullable: false),
                    Calories = table.Column<decimal>(type: "numeric", nullable: false),
                    Protein = table.Column<decimal>(type: "numeric", nullable: false),
                    Carbs = table.Column<decimal>(type: "numeric", nullable: false),
                    Fat = table.Column<decimal>(type: "numeric", nullable: false),
                    Fiber = table.Column<decimal>(type: "numeric", nullable: true),
                    Sugar = table.Column<decimal>(type: "numeric", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMealItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMealItems_NutritionCatalogItems_NutritionCatalogItemId",
                        column: x => x.NutritionCatalogItemId,
                        principalTable: "NutritionCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserMealItems_UserMeals_UserMealId",
                        column: x => x.UserMealId,
                        principalTable: "UserMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMealItems_NutritionCatalogItemId",
                table: "UserMealItems",
                column: "NutritionCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMealItems_UserMealId",
                table: "UserMealItems",
                column: "UserMealId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMeals_UserId_Date",
                table: "UserMeals",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMeals_UserId_MealType_Date",
                table: "UserMeals",
                columns: new[] { "UserId", "MealType", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMealItems");

            migrationBuilder.DropTable(
                name: "UserMeals");
        }
    }
}
