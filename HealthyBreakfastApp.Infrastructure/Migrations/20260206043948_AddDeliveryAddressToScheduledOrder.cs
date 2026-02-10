using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAddressToScheduledOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryAddressId",
                table: "ScheduledOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_DeliveryAddressId",
                table: "ScheduledOrders",
                column: "DeliveryAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledOrders_UserAddresses_DeliveryAddressId",
                table: "ScheduledOrders",
                column: "DeliveryAddressId",
                principalTable: "UserAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledOrders_UserAddresses_DeliveryAddressId",
                table: "ScheduledOrders");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledOrders_DeliveryAddressId",
                table: "ScheduledOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressId",
                table: "ScheduledOrders");
        }
    }
}
