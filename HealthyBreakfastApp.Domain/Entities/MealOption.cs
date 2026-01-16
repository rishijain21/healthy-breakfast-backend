using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class MealOption
    {
        [Key]
        public int MealOptionId { get; set; }

        [ForeignKey("Meal")]
        public int MealId { get; set; }

        [ForeignKey("IngredientCategory")]
        public int CategoryId { get; set; }

        public bool IsRequired { get; set; }
        public int MaxSelectable { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public Meal Meal { get; set; } = null!;
        public IngredientCategory IngredientCategory { get; set; } = null!;
        
        // ADD THIS: Navigation property
        public ICollection<MealOptionIngredient> MealOptionIngredients { get; set; } = new List<MealOptionIngredient>();
    }
}
