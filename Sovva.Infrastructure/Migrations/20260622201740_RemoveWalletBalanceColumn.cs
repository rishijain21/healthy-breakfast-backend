using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWalletBalanceColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Users\" DROP CONSTRAINT IF EXISTS \"CK_Users_WalletBalance\";");

            migrationBuilder.DropColumn(
                name: "WalletBalance",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WalletBalance",
                table: "Users",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                column: "WalletBalance",
                value: 625m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_WalletBalance",
                table: "Users",
                sql: "\"WalletBalance\" >= 0");
        }
    }
}
