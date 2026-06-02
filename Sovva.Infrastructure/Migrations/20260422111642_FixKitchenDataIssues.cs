using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixKitchenDataIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Fix Pomegranate emoji
            migrationBuilder.Sql(@"
                UPDATE public.""Ingredients""
                SET ""IconEmoji"" = '🍎'
                WHERE ""IngredientName"" = 'Pomegranate';
            ");

            // 2. Add realistic Fiber values for nuts
            migrationBuilder.Sql(@"
                UPDATE public.""Ingredients"" SET ""Fiber"" = 3.5 WHERE ""IngredientName"" = 'Almonds';
                UPDATE public.""Ingredients"" SET ""Fiber"" = 2.0 WHERE ""IngredientName"" = 'Walnuts';
                UPDATE public.""Ingredients"" SET ""Fiber"" = 1.0 WHERE ""IngredientName"" = 'Cashews';
                UPDATE public.""Ingredients"" SET ""Fiber"" = 3.0 WHERE ""IngredientName"" = 'Pistachios';
            ");

            // 3. Fix missing phone number for the test user ""Ram prasad""
            migrationBuilder.Sql(@"
                UPDATE public.""Users""
                SET ""Phone"" = '+919876543210'
                WHERE ""Name"" = 'Ram prasad' AND (""Phone"" = '' OR ""Phone"" IS NULL);
            ");

            // 4. Clean up broken test orders (orders created without a UserMeal, causing empty ingredients)
            migrationBuilder.Sql(@"
                DELETE FROM public.""Orders""
                WHERE ""UserMealId"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
