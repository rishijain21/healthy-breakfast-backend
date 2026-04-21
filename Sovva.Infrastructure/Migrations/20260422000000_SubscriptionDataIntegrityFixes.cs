using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionDataIntegrityFixesMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: First do a FORCE dedup - keeping ONE row per (UserId, MealId)
            // Using a temp table approach for reliability
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE temp_keep_ids AS
                SELECT MIN(""UserMealId"") as ""KeepId""
                FROM public.""UserMeals""
                GROUP BY ""UserId"", ""MealId"";
            ");
            
            migrationBuilder.Sql(@"
                DELETE FROM public.""UserMeals"" 
                WHERE ""UserMealId"" NOT IN (SELECT ""KeepId"" FROM temp_keep_ids);
            ");
            
            migrationBuilder.Sql(@"
                DROP TABLE temp_keep_ids;
            ");

            // Step 2: Now create unique index - this should work since dups are gone
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_UserMeals_UserId_MealId""
                ON public.""UserMeals"" (""UserId"", ""MealId"");
            ");

            // Step 3: Partial unique index for Subscriptions
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Subscriptions_ActiveUserMeal""
                ON public.""Subscriptions"" (""UserId"", ""UserMealId"")
                WHERE ""Active"" = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_Subscriptions_ActiveUserMeal"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_UserMeals_UserId_MealId"";");
        }
    }
}