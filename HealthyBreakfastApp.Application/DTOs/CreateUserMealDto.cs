namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateUserMealDto
    {
        public int UserId { get; set; }
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public decimal TotalPrice { get; set; }
    }
}
