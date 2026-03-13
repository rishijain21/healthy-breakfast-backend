using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_ScheduledFor",
                table: "Orders",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_Status",
                table: "Orders",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ScheduledFor",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_Status",
                table: "Orders");
        }
    }
}
