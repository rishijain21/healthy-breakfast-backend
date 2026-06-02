using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Meals");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserMeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Meals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 1,
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 2,
                column: "DeletedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 3,
                column: "DeletedAt",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserMeals");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Meals");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Meals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 1,
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 2,
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 3,
                column: "IsDeleted",
                value: false);
        }
    }
}
