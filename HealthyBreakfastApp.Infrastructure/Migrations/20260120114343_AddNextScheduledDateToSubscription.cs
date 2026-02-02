using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNextScheduledDateToSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_UserMeals_UserMealId",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "Frequency",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateOnly>(
                name: "NextScheduledDate",
                table: "Subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BasePrice",
                table: "Meals",
                type: "numeric(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "ApproxCalories",
                table: "Meals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApproxCarbs",
                table: "Meals",
                type: "numeric(5,1)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApproxFats",
                table: "Meals",
                type: "numeric(5,1)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApproxProtein",
                table: "Meals",
                type: "numeric(5,1)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 1,
                columns: new[] { "ApproxCalories", "ApproxCarbs", "ApproxFats", "ApproxProtein" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 2,
                columns: new[] { "ApproxCalories", "ApproxCarbs", "ApproxFats", "ApproxProtein" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 3,
                columns: new[] { "ApproxCalories", "ApproxCarbs", "ApproxFats", "ApproxProtein" },
                values: new object[] { null, null, null, null });

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_UserMeals_UserMealId",
                table: "Subscriptions",
                column: "UserMealId",
                principalTable: "UserMeals",
                principalColumn: "UserMealId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_UserMeals_UserMealId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "NextScheduledDate",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ApproxCalories",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "ApproxCarbs",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "ApproxFats",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "ApproxProtein",
                table: "Meals");

            migrationBuilder.AlterColumn<string>(
                name: "Frequency",
                table: "Subscriptions",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "BasePrice",
                table: "Meals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_UserMeals_UserMealId",
                table: "Subscriptions",
                column: "UserMealId",
                principalTable: "UserMeals",
                principalColumn: "UserMealId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
