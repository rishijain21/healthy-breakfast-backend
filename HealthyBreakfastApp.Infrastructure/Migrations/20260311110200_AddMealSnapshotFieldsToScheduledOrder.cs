using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMealSnapshotFieldsToScheduledOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MealId",
                table: "ScheduledOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MealImageUrl",
                table: "ScheduledOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MealId",
                table: "ScheduledOrders");

            migrationBuilder.DropColumn(
                name: "MealImageUrl",
                table: "ScheduledOrders");
        }
    }
}
