using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderRatingCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Rating",
                table: "Orders",
                sql: "\"Rating\" IS NULL OR (\"Rating\" >= 1 AND \"Rating\" <= 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Rating",
                table: "Orders");
        }
    }
}
