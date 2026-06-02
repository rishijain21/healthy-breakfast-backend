using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateScheduledOrderStatusCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ScheduledOrders"" 
                DROP CONSTRAINT IF EXISTS ""CK_ScheduledOrders_Status"";
                
                ALTER TABLE ""ScheduledOrders"" 
                ADD CONSTRAINT ""CK_ScheduledOrders_Status""
                CHECK (""OrderStatus"" IN ('Scheduled', 'Confirmed', 'Cancelled', 'Processed', 'Processing', 'Failed'));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ScheduledOrders_Status",
                table: "ScheduledOrders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScheduledOrders_Status",
                table: "ScheduledOrders",
                sql: "\"OrderStatus\" IN ('Scheduled', 'Confirmed', 'Cancelled', 'Processed')");
        }
    }
}
