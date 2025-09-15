using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserAuthMappingFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_auth_mapping",
                columns: table => new
                {
                    mapping_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    auth_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_auth_mapping", x => x.mapping_id);
                    table.ForeignKey(
                        name: "FK_user_auth_mapping_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_auth_mapping_auth_id",
                table: "user_auth_mapping",
                column: "auth_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_auth_mapping_user_id",
                table: "user_auth_mapping",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_auth_mapping");
        }
    }
}
