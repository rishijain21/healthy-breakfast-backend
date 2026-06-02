using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionScheduleCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionSchedule_DayOfWeek",
                table: "SubscriptionSchedules",
                sql: "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionSchedule_DayOfWeek",
                table: "SubscriptionSchedules");
        }
    }
}
