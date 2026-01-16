using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class IngredientCategory
    {
        [Key]  // ⬅️ MAKE SURE THIS IS HERE
        public int CategoryId { get; set; }
        
        public string CategoryName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation property
        public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
    }
}
