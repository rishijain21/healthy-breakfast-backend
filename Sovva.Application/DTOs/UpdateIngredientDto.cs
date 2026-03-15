namespace Sovva.Application.DTOs
{
    public class UpdateIngredientDto
    {
        public int CategoryId { get; set; }
        public string IngredientName { get; set; } = null!;
        public decimal Price { get; set; }
        public bool Available { get; set; }
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Fiber { get; set; }
        public string Description { get; set; } = null!;
        public string IconEmoji { get; set; } = null!;
    }
}
