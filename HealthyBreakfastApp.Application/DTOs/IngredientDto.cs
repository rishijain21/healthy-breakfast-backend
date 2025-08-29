namespace HealthyBreakfastApp.Application.DTOs
{
    public class IngredientDto
    {
        public int IngredientId { get; set; }
        public int CategoryId { get; set; }
        public string IngredientName { get; set; } = null!;
        public decimal Price { get; set; }
        public bool Available { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // ADD THESE NUTRITIONAL FIELDS:
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Fiber { get; set; }
        public string Description { get; set; } = null!;
        public string IconEmoji { get; set; } = null!;
    }
}
