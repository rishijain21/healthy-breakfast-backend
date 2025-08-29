namespace HealthyBreakfastApp.Application.DTOs
{
    public class UserMealDto
    {
        public int UserMealId { get; set; }
        public int UserId { get; set; }
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
