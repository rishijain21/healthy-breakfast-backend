namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateMealDto
    {
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
    }
}
