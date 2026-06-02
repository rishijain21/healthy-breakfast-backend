using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class MealOptionIngredient : BaseEntity
    {
        [Key]
        public int MealOptionIngredientId { get; set; }

        [ForeignKey("MealOption")]
        public int MealOptionId { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }

        // Navigation properties
        public MealOption MealOption { get; set; } = null!;
        public Ingredient Ingredient { get; set; } = null!;
    }
}
