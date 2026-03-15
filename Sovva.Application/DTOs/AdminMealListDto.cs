namespace Sovva.Application.DTOs
{
    public class AdminMealListDto
    {
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        public int MealOptionsCount { get; set; }
        public bool IsComplete { get; set; }
        
        // ✅ ADD NUTRITION FIELDS
        public int? ApproxCalories { get; set; }
        public decimal? ApproxProtein { get; set; }
        public decimal? ApproxCarbs { get; set; }
        public decimal? ApproxFats { get; set; }

        // Image URL for thumbnail display
        public string? ImageUrl { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
