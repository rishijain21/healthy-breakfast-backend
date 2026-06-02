using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledOrderIdToWalletTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduledOrderId",
                table: "WalletTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "WalletTransactions",
                keyColumn: "TransactionId",
                keyValue: 1,
                column: "ScheduledOrderId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ScheduledOrderId",
                table: "WalletTransactions",
                column: "ScheduledOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ScheduledOrderId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ScheduledOrderId",
                table: "WalletTransactions");
        }
    }
}
