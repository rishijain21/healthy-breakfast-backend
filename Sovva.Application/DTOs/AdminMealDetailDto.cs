namespace Sovva.Application.DTOs
{
    public class AdminMealDetailDto
    {
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        
        // ✅ ADD NUTRITION FIELDS
        public int? ApproxCalories { get; set; }
        public decimal? ApproxProtein { get; set; }
        public decimal? ApproxCarbs { get; set; }
        public decimal? ApproxFats { get; set; }

        // Image URL for display
        public string? ImageUrl { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<AdminMealOptionDetailDto> MealOptions { get; set; } = new();
    }

    public class AdminMealOptionDetailDto
    {
        public int MealOptionId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public bool IsRequired { get; set; }
        public int MaxSelectable { get; set; }
        public List<MealIngredientDto> Ingredients { get; set; } = new();
    }

    public class MealIngredientDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = null!;
        public decimal Price { get; set; }
        public string IconEmoji { get; set; } = null!;
        public bool Available { get; set; }
        
        // ✅ OPTIONAL: Add individual ingredient nutrition (if you want to show it)
        public int? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Fiber { get; set; }
    }
}
