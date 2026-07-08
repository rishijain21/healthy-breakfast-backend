using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletCompositeIndexCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_UserId_Type_Amount",
                table: "WalletTransactions");

            // Use IF EXISTS — on a fresh database this index was never created by a prior migration
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_Subscriptions_Active_NextScheduledDate\";");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId_Type_Amount_CreatedAt",
                table: "WalletTransactions",
                columns: new[] { "UserId", "Type" })
                .Annotation("Npgsql:IndexInclude", new[] { "Amount", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Active_NextScheduledDate",
                table: "Subscriptions",
                columns: new[] { "IsActive", "NextScheduledDate" },
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_ScheduledFor_Unprocessed",
                table: "ScheduledOrders",
                columns: new[] { "ScheduledFor", "IsProcessedToOrder" },
                filter: "\"IsProcessedToOrder\" = false");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_UserId_Type_Amount_CreatedAt",
                table: "WalletTransactions");

            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_Subscriptions_Active_NextScheduledDate\";");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledOrders_ScheduledFor_Unprocessed",
                table: "ScheduledOrders");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId_Type_Amount",
                table: "WalletTransactions",
                columns: new[] { "UserId", "Type" })
                .Annotation("Npgsql:IndexInclude", new[] { "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Active_NextScheduledDate",
                table: "Subscriptions",
                columns: new[] { "IsActive", "NextScheduledDate" },
                filter: "\"Active\" = true");
        }
    }
}

