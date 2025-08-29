using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class MealOptionIngredient
    {
        [Key]
        public int MealOptionIngredientId { get; set; }

        [ForeignKey("MealOption")]
        public int MealOptionId { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public MealOption MealOption { get; set; } = null!;
        public Ingredient Ingredient { get; set; } = null!;
    }
}
