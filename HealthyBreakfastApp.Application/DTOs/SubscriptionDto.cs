namespace HealthyBreakfastApp.Application.DTOs
{
    public class SubscriptionDto
    {
        public int SubscriptionId { get; set; }
        public int UserId { get; set; }
        public int UserMealId { get; set; }
        public string Frequency { get; set; } = null!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties for display
        public string UserName { get; set; } = null!;
        public string MealName { get; set; } = null!;
        public decimal MealPrice { get; set; }
    }
}
