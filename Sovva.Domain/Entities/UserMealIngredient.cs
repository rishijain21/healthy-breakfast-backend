using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class UserMealIngredient : BaseEntity
    {
        [Key]
        public int UserMealIngredientId { get; set; }

        [ForeignKey("UserMeal")]
        public int UserMealId { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        // Navigation properties
        public UserMeal UserMeal { get; set; } = null!;
        public Ingredient Ingredient { get; set; } = null!;
    }
}
