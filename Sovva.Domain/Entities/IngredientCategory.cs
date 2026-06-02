using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sovva.Domain.Entities
{
    public class IngredientCategory : BaseEntity
    {
        [Key]  // ⬅️ MAKE SURE THIS IS HERE
        public int CategoryId { get; set; }
        
        public string CategoryName { get; set; } = null!;
        
        // Navigation property
        public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
    }
}
