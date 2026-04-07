using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceIdToWalletTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "WalletTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceId",
                table: "WalletTransactions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReferenceType", table: "WalletTransactions");
            migrationBuilder.DropColumn(name: "ReferenceId", table: "WalletTransactions");
        }
    }
}
