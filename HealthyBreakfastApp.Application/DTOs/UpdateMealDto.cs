namespace HealthyBreakfastApp.Application.DTOs
{
    public class UpdateMealDto
    {
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        
        // ✅ ADD NUTRITION FIELDS
        public int? ApproxCalories { get; set; }
        public decimal? ApproxProtein { get; set; }
        public decimal? ApproxCarbs { get; set; }
        public decimal? ApproxFats { get; set; }
        
        public List<AdminMealOptionDto> MealOptions { get; set; } = new();
    }
}
