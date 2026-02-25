namespace HealthyBreakfastApp.Application.DTOs
{
    public class MealWithDetailsDto
    {
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        public int? ApproxCalories { get; set; }
        public decimal? ApproxProtein { get; set; }
        public decimal? ApproxCarbs { get; set; }
        public decimal? ApproxFats { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int MealOptionsCount { get; set; }
        public List<MealOptionDto> MealOptions { get; set; } = new();
    }
}
