using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DualTargetSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_ActiveUserMeal",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "UserMealId",
                table: "Subscriptions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedPrice",
                table: "Subscriptions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MealId",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PauseReason",
                table: "Subscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_MealId",
                table: "Subscriptions",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_ActiveMeal",
                table: "Subscriptions",
                columns: new[] { "UserId", "MealId" },
                unique: true,
                filter: "\"IsActive\" = true AND \"MealId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_ActiveUserMeal",
                table: "Subscriptions",
                columns: new[] { "UserId", "UserMealId" },
                unique: true,
                filter: "\"IsActive\" = true AND \"UserMealId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_MealType",
                table: "Subscriptions",
                sql: "(\"MealId\" IS NOT NULL AND \"UserMealId\" IS NULL) OR (\"MealId\" IS NULL AND \"UserMealId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Meals_MealId",
                table: "Subscriptions",
                column: "MealId",
                principalTable: "Meals",
                principalColumn: "MealId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Meals_MealId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_MealId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_ActiveMeal",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_ActiveUserMeal",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_MealType",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "AgreedPrice",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "MealId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PauseReason",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "UserMealId",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_ActiveUserMeal",
                table: "Subscriptions",
                columns: new[] { "UserId", "UserMealId" },
                unique: true,
                filter: "\"Active\" = true");
        }
    }
}
