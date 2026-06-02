namespace Sovva.Application.DTOs
{
    public class IngredientCategoryDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
