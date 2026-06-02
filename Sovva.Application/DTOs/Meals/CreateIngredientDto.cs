using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class CreateIngredientDto
    {
        [Required]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(255, MinimumLength = 2)]
        public string IngredientName { get; set; } = null!;

        [Required]
        [Range(0, 10000)]
        public decimal Price { get; set; }

        public bool Available { get; set; } = true;

        // Nutritional info
        [Range(0, 10000)]
        public int Calories { get; set; }

        [Range(0, 1000)]
        public decimal Protein { get; set; }

        [Range(0, 1000)]
        public decimal Fiber { get; set; }

        // Display info
        [StringLength(10)]
        public string? IconEmoji { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
