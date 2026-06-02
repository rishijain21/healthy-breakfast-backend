namespace Sovva.Application.DTOs
{
    public class AdminCreateMealDto
    {
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        
        // ✅ ADD NUTRITION FIELDS
        public int? ApproxCalories { get; set; }
        public decimal? ApproxProtein { get; set; }
        public decimal? ApproxCarbs { get; set; }
        public decimal? ApproxFats { get; set; }

        // Optional: Image file to upload (base64 or handled via separate endpoint)
        // For separate upload endpoint, this can be null
        public List<AdminMealOptionDto> MealOptions { get; set; } = new();
    }

    public class AdminMealOptionDto
    {
        public int CategoryId { get; set; }
        public bool IsRequired { get; set; }
        public int MaxSelectable { get; set; }
        public List<int> IngredientIds { get; set; } = new();
    }
}
