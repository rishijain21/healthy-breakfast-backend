namespace Sovva.Application.DTOs
{
    public class IngredientDto
    {
        public int IngredientId { get; set; }
        public int CategoryId { get; set; }
        public string IngredientName { get; set; } = null!;
        public decimal Price { get; set; }
        public bool Available { get; set; }
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Fiber { get; set; }
        public string IconEmoji { get; set; } = null!;
        public string Description { get; set; } = null!;
        // Remove CreatedAt and UpdatedAt - they're not needed in DTOs
    }
}
