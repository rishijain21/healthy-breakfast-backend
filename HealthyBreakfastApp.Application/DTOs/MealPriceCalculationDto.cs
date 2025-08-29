using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class MealPriceCalculationDto
    {
        [Required]
        public int MealId { get; set; }
        
        [Required]
        public List<SelectedIngredientDto> SelectedIngredients { get; set; } = new();
    }

    public class SelectedIngredientDto
    {
        [Required]
        public int IngredientId { get; set; }
        
        [Required]
        [Range(1, 10, ErrorMessage = "Quantity must be between 1 and 10")]
        public int Quantity { get; set; }
    }
}
