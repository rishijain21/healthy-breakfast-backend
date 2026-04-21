using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionDataIntegrityFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMeals_Meals_MealId",
                table: "UserMeals");

            migrationBuilder.DropIndex(
                name: "IX_UserMeals_UserId",
                table: "UserMeals");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPrice",
                table: "UserMeals",
                type: "numeric(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "MealName",
                table: "UserMeals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "UX_UserMeals_UserId_MealId",
                table: "UserMeals",
                columns: new[] { "UserId", "MealId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_ActiveUserMeal",
                table: "Subscriptions",
                columns: new[] { "UserId", "UserMealId" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMeals_Meals_MealId",
                table: "UserMeals",
                column: "MealId",
                principalTable: "Meals",
                principalColumn: "MealId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMeals_Meals_MealId",
                table: "UserMeals");

            migrationBuilder.DropIndex(
                name: "UX_UserMeals_UserId_MealId",
                table: "UserMeals");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_ActiveUserMeal",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPrice",
                table: "UserMeals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)");

            migrationBuilder.AlterColumn<string>(
                name: "MealName",
                table: "UserMeals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_UserMeals_UserId",
                table: "UserMeals",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMeals_Meals_MealId",
                table: "UserMeals",
                column: "MealId",
                principalTable: "Meals",
                principalColumn: "MealId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
