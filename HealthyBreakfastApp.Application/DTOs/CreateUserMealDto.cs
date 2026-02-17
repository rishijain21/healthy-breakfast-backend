namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateUserMealDto
    {
        // UserId is passed as separate parameter to service (not in DTO)
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // ✅ NEW: Selected ingredients for subscription
        public List<CreateUserMealIngredientDto>? SelectedIngredients { get; set; }
    }
}
