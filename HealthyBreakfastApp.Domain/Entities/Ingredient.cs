using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class Ingredient
    {
        [Key]
        public int IngredientId { get; set; }

        [ForeignKey("IngredientCategory")]
        public int CategoryId { get; set; }                       // FK to IngredientCategory

        public string IngredientName { get; set; } = null!;
        public decimal Price { get; set; }
        public bool Available { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Nutritional properties - These are what your Angular UI needs
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Fiber { get; set; }
        
        public string Description { get; set; } = null!;
        public string IconEmoji { get; set; } = null!;

        // Navigation property
        public IngredientCategory IngredientCategory { get; set; } = null!;
    }
}
