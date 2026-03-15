using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
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
        public int IngredientId { get; set; }
        public int Quantity { get; set; }

        /// <summary>
        /// Snapshot unit price from the scheduled order ingredient.
        /// Only populated when called from the midnight job.
        /// </summary>
        public decimal? UnitPrice { get; set; }

        /// <summary>
        /// Snapshot total price (UnitPrice × Quantity) from the scheduled order.
        /// Only populated when called from the midnight job.
        /// </summary>
        public decimal? TotalPrice { get; set; }
    }
}
