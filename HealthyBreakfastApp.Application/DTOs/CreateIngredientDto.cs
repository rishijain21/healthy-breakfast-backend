namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateIngredientDto
    {
        public int CategoryId { get; set; }
        public string IngredientName { get; set; } = null!;
        public decimal Price { get; set; }
        public bool Available { get; set; }
    }
}
