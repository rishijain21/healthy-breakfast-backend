using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyAndWalletIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ScheduledOrderId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId_Type_Amount",
                table: "WalletTransactions",
                columns: new[] { "UserId", "Type" })
                .Annotation("Npgsql:IndexInclude", new[] { "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ScheduledOrderId",
                table: "Orders",
                column: "ScheduledOrderId",
                unique: true,
                filter: "\"ScheduledOrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_UserId_Type_Amount",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ScheduledOrderId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ScheduledOrderId",
                table: "Orders",
                column: "ScheduledOrderId",
                unique: true);
        }
    }
}
