namespace Sovva.Application.DTOs
{
    public class CreateMealDto
    {
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        
        // ✅ ADD NUTRITION FIELDS
        public int? ApproxCalories { get; set; }
        public decimal? ApproxProtein { get; set; }
        public decimal? ApproxCarbs { get; set; }
        public decimal? ApproxFats { get; set; }
    }
}
