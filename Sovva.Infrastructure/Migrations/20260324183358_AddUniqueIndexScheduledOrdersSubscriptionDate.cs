using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexScheduledOrdersSubscriptionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduledOrders_SubscriptionId",
                table: "ScheduledOrders");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique",
                table: "ScheduledOrders",
                columns: new[] { "SubscriptionId", "ScheduledFor" },
                unique: true,
                filter: "\"SubscriptionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique",
                table: "ScheduledOrders");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_SubscriptionId",
                table: "ScheduledOrders",
                column: "SubscriptionId");
        }
    }
}
