using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeIngredientNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Industry Standard: Setting accurate nutritional values per standard serving size
-- Oats / Bases (Standard Serving: 40g dry)
UPDATE public.""Ingredients"" SET ""Calories""=150, ""Protein""=5.0, ""Fiber""=4.0, ""Price""=10, ""Description""='(Per 40g serving) Classic choice, quick cooking' WHERE ""IngredientName"" = 'Rolled Oats';
UPDATE public.""Ingredients"" SET ""Calories""=145, ""Protein""=5.0, ""Fiber""=4.0, ""Price""=14, ""Description""='(Per 40g serving) High fiber, slow release energy' WHERE ""IngredientName"" = 'Steel Cut Oats';
UPDATE public.""Ingredients"" SET ""Calories""=150, ""Protein""=4.0, ""Fiber""=3.0, ""Price""=12, ""Description""='(Per 40g serving) Quick cooking convenience' WHERE ""IngredientName"" = 'Instant Oats';
UPDATE public.""Ingredients"" SET ""Calories""=98,  ""Protein""=7.0, ""Fiber""=6.0, ""Price""=16, ""Description""='(Per 40g serving) High fiber superfood' WHERE ""IngredientName"" = 'Oat Bran';

-- Seeds (Standard Serving: 15g / ~1 tablespoon)
UPDATE public.""Ingredients"" SET ""Calories""=73, ""Protein""=2.5, ""Fiber""=5.0, ""Price""=8, ""Description""='(Per 15g serving) Rich in Omega-3; adds pudding-like texture' WHERE ""IngredientName"" = 'Chia Seeds';
UPDATE public.""Ingredients"" SET ""Calories""=80, ""Protein""=2.8, ""Fiber""=4.0, ""Price""=6, ""Description""='(Per 15g serving) High in lignans; best ground' WHERE ""IngredientName"" = 'Flax Seeds';
UPDATE public.""Ingredients"" SET ""Calories""=85, ""Protein""=4.5, ""Fiber""=1.0, ""Price""=10, ""Description""='(Per 15g serving) Packed with magnesium & zinc' WHERE ""IngredientName"" = 'Pumpkin Seeds';
UPDATE public.""Ingredients"" SET ""Calories""=87, ""Protein""=3.0, ""Fiber""=1.5, ""Price""=5, ""Description""='(Per 15g serving) Loaded with Vitamin E' WHERE ""IngredientName"" = 'Sunflower Seeds';
UPDATE public.""Ingredients"" SET ""Calories""=85, ""Protein""=2.5, ""Fiber""=1.7, ""Price""=7, ""Description""='(Per 15g serving) Source of calcium & iron' WHERE ""IngredientName"" = 'Sesame Seeds';

-- Nuts (Standard Serving: 15g / small handful)
UPDATE public.""Ingredients"" SET ""Calories""=87, ""Protein""=3.2, ""Fiber""=1.8, ""Price""=12, ""Description""='(Per 15g serving) High in Vitamin E & protein' WHERE ""IngredientName"" = 'Almonds';
UPDATE public.""Ingredients"" SET ""Calories""=98, ""Protein""=2.3, ""Fiber""=1.0, ""Price""=15, ""Description""='(Per 15g serving) High in Omega-3 (ALA)' WHERE ""IngredientName"" = 'Walnuts';
UPDATE public.""Ingredients"" SET ""Calories""=83, ""Protein""=2.7, ""Fiber""=0.5, ""Price""=14, ""Description""='(Per 15g serving) Rich in magnesium & iron' WHERE ""IngredientName"" = 'Cashews';
UPDATE public.""Ingredients"" SET ""Calories""=85, ""Protein""=3.0, ""Fiber""=1.5, ""Price""=16, ""Description""='(Per 15g serving) High in potassium & B-6' WHERE ""IngredientName"" = 'Pistachios';
UPDATE public.""Ingredients"" SET ""Calories""=45, ""Protein""=0.5, ""Fiber""=0.6, ""Price""=6, ""Description""='(Per 15g serving) Natural sweetness + iron' WHERE ""IngredientName"" = 'Raisins';

-- Fruits (Standard Serving: 50g)
UPDATE public.""Ingredients"" SET ""Calories""=45, ""Protein""=0.5, ""Fiber""=1.3, ""Price""=8, ""Description""='(Per 50g serving) Rich in potassium' WHERE ""IngredientName"" = 'Sliced Banana';
UPDATE public.""Ingredients"" SET ""Calories""=26, ""Protein""=0.1, ""Fiber""=1.2, ""Price""=10, ""Description""='(Per 50g serving) Good source of Vitamin C' WHERE ""IngredientName"" = 'Apple Chunks';
UPDATE public.""Ingredients"" SET ""Calories""=42, ""Protein""=0.8, ""Fiber""=2.0, ""Price""=18, ""Description""='(Per 50g serving) Packed with antioxidants' WHERE ""IngredientName"" = 'Pomegranate';
UPDATE public.""Ingredients"" SET ""Calories""=22, ""Protein""=0.2, ""Fiber""=0.9, ""Price""=9, ""Description""='(Per 50g serving) Digestive enzymes' WHERE ""IngredientName"" = 'Papaya';
UPDATE public.""Ingredients"" SET ""Calories""=30, ""Protein""=0.4, ""Fiber""=0.8, ""Price""=15, ""Description""='(Per 50g serving) Boosts immunity' WHERE ""IngredientName"" = 'Mango';
UPDATE public.""Ingredients"" SET ""Calories""=35, ""Protein""=0.4, ""Fiber""=0.5, ""Price""=12, ""Description""='(Per 50g serving) Supports heart health' WHERE ""IngredientName"" = 'Grapes';
UPDATE public.""Ingredients"" SET ""Calories""=16, ""Protein""=0.3, ""Fiber""=1.0, ""Price""=14, ""Description""='(Per 50g serving) Fresh sweetness' WHERE ""IngredientName"" = 'Strawberries';
UPDATE public.""Ingredients"" SET ""Calories""=140, ""Protein""=1.0, ""Fiber""=4.0, ""Price""=12, ""Description""='(Per 50g serving) Natural energy' WHERE ""IngredientName"" = 'Dates (Khajoor)';

-- Milks (Standard Serving: 200ml)
UPDATE public.""Ingredients"" SET ""Calories""=195, ""Protein""=7.5, ""Fiber""=0.0, ""Price""=22, ""Description""='(Per 200ml serving) Creamy & energy-dense' WHERE ""IngredientName"" = 'Full Cream Buffalo Milk';
UPDATE public.""Ingredients"" SET ""Calories""=120, ""Protein""=6.5, ""Fiber""=0.0, ""Price""=20, ""Description""='(Per 200ml serving) Rich in calcium' WHERE ""IngredientName"" = 'Full Cream Cow''s Milk';
UPDATE public.""Ingredients"" SET ""Calories""=85,  ""Protein""=6.5, ""Fiber""=0.0, ""Price""=18, ""Description""='(Per 200ml serving) Heart-friendly choice' WHERE ""IngredientName"" = 'Low-Fat Cow''s Milk';
UPDATE public.""Ingredients"" SET ""Calories""=140, ""Protein""=8.0, ""Fiber""=0.0, ""Price""=20, ""Description""='(Per 200ml serving) Good balance of protein' WHERE ""IngredientName"" = 'Low-Fat Buffalo Milk';
UPDATE public.""Ingredients"" SET ""Calories""=80,  ""Protein""=6.0, ""Fiber""=1.0, ""Price""=25, ""Description""='(Per 200ml serving) Lactose-free; plant protein' WHERE ""IngredientName"" = 'Soy Milk (Unsweetened)';
UPDATE public.""Ingredients"" SET ""Calories""=30,  ""Protein""=1.0, ""Fiber""=0.0, ""Price""=35, ""Description""='(Per 200ml serving) Light & nutty; low calorie' WHERE ""IngredientName"" = 'Almond Milk (Unsweetened)';
UPDATE public.""Ingredients"" SET ""Calories""=90,  ""Protein""=2.0, ""Fiber""=1.5, ""Price""=40, ""Description""='(Per 200ml serving) Creamy texture; beta-glucan' WHERE ""IngredientName"" = 'Oat Milk (Unsweetened)';
UPDATE public.""Ingredients"" SET ""Calories""=120, ""Protein""=1.0, ""Fiber""=0.0, ""Price""=30, ""Description""='(Per 200ml serving) Rich in MCTs' WHERE ""IngredientName"" = 'Coconut Milk (Light)';

-- Sweeteners (Standard Serving: 10g / ~2 teaspoons)
UPDATE public.""Ingredients"" SET ""Calories""=30, ""Protein""=0.0, ""Fiber""=0.0, ""Price""=5, ""Description""='(Per 10g serving) Floral sweetness' WHERE ""IngredientName"" = 'Honey';
UPDATE public.""Ingredients"" SET ""Calories""=38, ""Protein""=0.0, ""Fiber""=0.0, ""Price""=4, ""Description""='(Per 10g serving) Earthy sweetness + minerals' WHERE ""IngredientName"" = 'Jaggery';
UPDATE public.""Ingredients"" SET ""Calories""=0,  ""Protein""=0.0, ""Fiber""=0.0, ""Price""=0, ""Description""='Natural fruit sweetness only' WHERE ""IngredientName"" = 'No Sweetener';

-- Add-ons & Extras
UPDATE public.""Ingredients"" SET ""Calories""=5,   ""Protein""=0.0, ""Fiber""=0.0, ""Price""=5,  ""Description""='(Per 30ml shot) Rich espresso flavor' WHERE ""IngredientName"" = 'Coffee Decoction';
UPDATE public.""Ingredients"" SET ""Calories""=15,  ""Protein""=1.0, ""Fiber""=1.5, ""Price""=6,  ""Description""='(Per 5g serving) Chocolate flavor' WHERE ""IngredientName"" = 'Cocoa Powder';
UPDATE public.""Ingredients"" SET ""Calories""=100, ""Protein""=10.0, ""Fiber""=0.0, ""Price""=25, ""Description""='(Per 100g serving) High-protein probiotic' WHERE ""IngredientName"" = 'Greek Yogurt';
UPDATE public.""Ingredients"" SET ""Calories""=115, ""Protein""=24.0, ""Fiber""=0.0, ""Price""=40, ""Description""='(Per 30g scoop) Premium muscle recovery' WHERE ""IngredientName"" = 'Chocolate Whey Protein';
UPDATE public.""Ingredients"" SET ""Calories""=60,  ""Protein""=1.5, ""Fiber""=2.0, ""Price""=10, ""Description""='(Per 15g serving) Crunchy and light' WHERE ""IngredientName"" = 'Roasted Makhana (Foxnuts)';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
