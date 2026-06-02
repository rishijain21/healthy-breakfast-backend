using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "UserMealIngredients",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "UserMealIngredients",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Meals_IsDeleted",
                table: "Meals",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_IsComplete",
                table: "Meals",
                column: "IsComplete");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceableLocations_IsActive",
                table: "ServiceableLocations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_IsPrimary",
                table: "UserAddresses",
                column: "IsPrimary",
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_IsActive",
                table: "UserAddresses",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Meals_IsDeleted", table: "Meals");
            migrationBuilder.DropIndex(name: "IX_Meals_IsComplete", table: "Meals");
            migrationBuilder.DropIndex(name: "IX_ServiceableLocations_IsActive", table: "ServiceableLocations");
            migrationBuilder.DropIndex(name: "IX_UserAddresses_IsPrimary", table: "UserAddresses");
            migrationBuilder.DropIndex(name: "IX_UserAddresses_IsActive", table: "UserAddresses");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "UserMealIngredients");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "UserMealIngredients");
        }
    }
}
