namespace HealthyBreakfastApp.Application.DTOs
{
    public class UserMealIngredientDto
    {
        public int UserMealIngredientId { get; set; }
        public int UserMealId { get; set; }
        public int IngredientId { get; set; }
        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
