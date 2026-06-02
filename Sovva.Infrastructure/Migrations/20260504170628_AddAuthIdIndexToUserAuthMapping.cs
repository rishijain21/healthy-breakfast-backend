using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthIdIndexToUserAuthMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserAuthMapping_AuthId",
                table: "user_auth_mapping",
                column: "auth_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAuthMapping_AuthId",
                table: "user_auth_mapping");
        }
    }
}
