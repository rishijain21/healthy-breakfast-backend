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

    public class IngredientBreakdownDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        public string CategoryName { get; set; } = null!;
    }
}
