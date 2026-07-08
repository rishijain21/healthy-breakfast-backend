using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledOrderIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ScheduledOrderId",
                table: "WalletTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ScheduledOrderId",
                table: "WalletTransactions",
                column: "ScheduledOrderId",
                filter: "\"ScheduledOrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ScheduledOrderId",
                table: "WalletTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ScheduledOrderId",
                table: "WalletTransactions",
                column: "ScheduledOrderId");
        }
    }
}
