using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMealOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealOptions",
                columns: table => new
                {
                    MealOptionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MaxSelectable = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealOptions", x => x.MealOptionId);
                    table.ForeignKey(
                        name: "FK_MealOptions_IngredientCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IngredientCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealOptions_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "MealId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealOptionIngredients",
                columns: table => new
                {
                    MealOptionIngredientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealOptionId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealOptionIngredients", x => x.MealOptionIngredientId);
                    table.ForeignKey(
                        name: "FK_MealOptionIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealOptionIngredients_MealOptions_MealOptionId",
                        column: x => x.MealOptionId,
                        principalTable: "MealOptions",
                        principalColumn: "MealOptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealOptionIngredients_IngredientId",
                table: "MealOptionIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_MealOptionIngredients_MealOptionId",
                table: "MealOptionIngredients",
                column: "MealOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MealOptions_CategoryId",
                table: "MealOptions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MealOptions_MealId",
                table: "MealOptions",
                column: "MealId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealOptionIngredients");

            migrationBuilder.DropTable(
                name: "MealOptions");
        }
    }
}
