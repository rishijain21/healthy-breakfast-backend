using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FreshNutritionalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Calories",
                table: "Ingredients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Ingredients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Fiber",
                table: "Ingredients",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "IconEmoji",
                table: "Ingredients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Protein",
                table: "Ingredients",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.InsertData(
                table: "IngredientCategories",
                columns: new[] { "CategoryId", "CategoryName", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Oats", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Seeds", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "Fruits", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "Milk", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "Sweetener", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Meals",
                columns: new[] { "MealId", "BasePrice", "CreatedAt", "Description", "MealName", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 40m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Traditional overnight oats base", "Classic Overnight Oats", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 50m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Build your perfect breakfast", "Custom Breakfast Bowl", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 60m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "High protein breakfast option", "Protein Power Bowl", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatedAt", "Email", "Name", "Phone", "UpdatedAt", "WalletBalance" },
                values: new object[] { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "testuser@healthybreakfast.com", "TestUser", "1234567890", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 625m });

            migrationBuilder.InsertData(
                table: "Ingredients",
                columns: new[] { "IngredientId", "Available", "Calories", "CategoryId", "CreatedAt", "Description", "Fiber", "IconEmoji", "IngredientName", "Price", "Protein", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, true, 120, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "High fiber, slow release energy", 4m, "🥣", "Steel Cut Oats", 25m, 4m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, true, 150, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Classic choice, quick cooking", 4m, "🌾", "Rolled Oats", 20m, 5m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, true, 110, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Complete protein, gluten-free", 3m, "🌱", "Quinoa Flakes", 40m, 6m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, true, 60, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Omega-3 rich superfood", 5m, "🌰", "Chia Seeds", 15m, 3m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, true, 55, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Heart healthy fiber", 3m, "🌾", "Flax Seeds", 12m, 2m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, true, 85, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Magnesium powerhouse", 1m, "🎃", "Pumpkin Seeds", 20m, 4m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, true, 42, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Antioxidant rich blueberries", 2m, "🫐", "Fresh Blueberries", 45m, 1m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, true, 90, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Natural sweetness & potassium", 3m, "🍌", "Sliced Banana", 15m, 1m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, true, 50, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Crispy texture & fiber", 2m, "🍎", "Apple Chunks", 25m, 0m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 30, true, 40, 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Light and nutty plant milk", 0m, "🥛", "Almond Milk", 20m, 1m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 32, true, 100, 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "High protein probiotic", 0m, "🥄", "Greek Yogurt", 35m, 15m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 40, true, 65, 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Natural unprocessed sweetener", 0m, "🍯", "Raw Honey", 12m, 0m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 44, true, 0, 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Natural fruit sweetness only", 0m, "🚫", "No Sweetener", 0m, 0m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "WalletTransactions",
                columns: new[] { "TransactionId", "Amount", "CreatedAt", "Description", "Type", "UserId" },
                values: new object[] { 1, 625m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Initial wallet balance", "Credit", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "IngredientId",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Meals",
                keyColumn: "MealId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "WalletTransactions",
                keyColumn: "TransactionId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "CategoryId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "CategoryId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "CategoryId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "CategoryId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "CategoryId",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "Calories",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Fiber",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "IconEmoji",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Protein",
                table: "Ingredients");
        }
    }
}
