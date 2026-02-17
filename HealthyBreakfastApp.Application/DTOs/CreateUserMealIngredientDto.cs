namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateUserMealIngredientDto
    {
        public int? UserMealId { get; set; } // Optional - set by service
        public int IngredientId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
