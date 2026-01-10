using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenDashboardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tables and columns already created manually in database
            // This migration is just to sync EF Core metadata
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledOrderIngredients");

            migrationBuilder.DropTable(
                name: "ScheduledOrders");

            migrationBuilder.DropColumn(
                name: "IsPrepared",
                table: "Orders");
        }
    }
}
