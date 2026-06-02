namespace Sovva.Application.DTOs
{
    public class MealOptionIngredientDto
    {
        public int MealOptionIngredientId { get; set; }
        public int MealOptionId { get; set; }
        public int IngredientId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
