namespace Sovva.Application.DTOs
{
    public class CategoryWithIngredientsDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public List<IngredientDto> Ingredients { get; set; } = new();
    }
}
