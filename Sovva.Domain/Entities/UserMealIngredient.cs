using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class UserMealIngredient
    {
        [Key]
        public int UserMealIngredientId { get; set; }

        [ForeignKey("UserMeal")]
        public int UserMealId { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }

        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public UserMeal UserMeal { get; set; } = null!;
        public Ingredient Ingredient { get; set; } = null!;
    }
}
