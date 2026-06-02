using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWalletBalanceConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "user_auth_mapping",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "Active",
                table: "Subscriptions",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "Available",
                table: "Ingredients",
                newName: "IsAvailable");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "WalletTransactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserAddresses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "user_auth_mapping",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ServiceableLocations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ScheduledOrderIngredients",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "WalletTransactions",
                keyColumn: "TransactionId",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_ScheduledOrders_ScheduledOrderId",
                table: "WalletTransactions",
                column: "ScheduledOrderId",
                principalTable: "ScheduledOrders",
                principalColumn: "ScheduledOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_ScheduledOrders_ScheduledOrderId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "user_auth_mapping");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ScheduledOrderIngredients");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "user_auth_mapping",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Subscriptions",
                newName: "Active");

            migrationBuilder.RenameColumn(
                name: "IsAvailable",
                table: "Ingredients",
                newName: "Available");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserAddresses",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ServiceableLocations",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
