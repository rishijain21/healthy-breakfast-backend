namespace HealthyBreakfastApp.Application.DTOs
{
    public class BatchMealRequestDto
    {
        public List<int> MealIds { get; set; } = new();
    }
}
