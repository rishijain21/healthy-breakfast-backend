using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrgmIndexesForSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS IX_ServiceableLocations_City_Trgm
                ON ""ServiceableLocations"" USING GIN (""City"" gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS IX_ServiceableLocations_Pincode_Trgm
                ON ""ServiceableLocations"" USING GIN (""Pincode"" gin_trgm_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS IX_ServiceableLocations_City_Trgm;
                DROP INDEX IF EXISTS IX_ServiceableLocations_Pincode_Trgm;
            ");
        }
    }
}
