using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionIdToScheduledOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubscriptionId",
                table: "ScheduledOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_SubscriptionId",
                table: "ScheduledOrders",
                column: "SubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledOrders_Subscriptions_SubscriptionId",
                table: "ScheduledOrders",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "SubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledOrders_Subscriptions_SubscriptionId",
                table: "ScheduledOrders");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledOrders_SubscriptionId",
                table: "ScheduledOrders");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "ScheduledOrders");
        }
    }
}
