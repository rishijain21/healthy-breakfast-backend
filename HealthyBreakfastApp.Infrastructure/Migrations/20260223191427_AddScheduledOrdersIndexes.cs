using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledOrdersIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Composite index — covers both authId filter + date range in one scan
            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_AuthId_ScheduledFor",
                table: "ScheduledOrders",
                columns: new[] { "AuthId", "ScheduledFor" });

            // Separate index for the midnight job (queries by date only, no authId)
            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_ScheduledFor",
                table: "ScheduledOrders",
                column: "ScheduledFor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_ScheduledOrders_AuthId_ScheduledFor", "ScheduledOrders");
            migrationBuilder.DropIndex("IX_ScheduledOrders_ScheduledFor", "ScheduledOrders");
        }
    }
}
