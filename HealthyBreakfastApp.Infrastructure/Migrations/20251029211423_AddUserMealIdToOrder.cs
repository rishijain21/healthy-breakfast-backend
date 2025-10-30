using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMealIdToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserMealId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserMealId",
                table: "Orders",
                column: "UserMealId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_UserMeals_UserMealId",
                table: "Orders",
                column: "UserMealId",
                principalTable: "UserMeals",
                principalColumn: "UserMealId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_UserMeals_UserMealId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserMealId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UserMealId",
                table: "Orders");
        }
    }
}
