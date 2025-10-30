namespace HealthyBreakfastApp.Application.DTOs
{
    public class MealPriceResponseDto
    {
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public decimal BaseMealPrice { get; set; }
        public decimal IngredientsPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalFiber { get; set; }
        public List<IngredientBreakdownDto> IngredientBreakdown { get; set; } = new();
    }

    // ✅ REMOVED: IngredientBreakdownDto class (exists in separate file)
}
