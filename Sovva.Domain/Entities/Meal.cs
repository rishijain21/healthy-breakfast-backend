using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class Meal
    {
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        
        [Column(TypeName = "decimal(10,2)")]
        public decimal BasePrice { get; set; }
        
        // ✅ ADD NUTRITION FIELDS
        public int? ApproxCalories { get; set; }
        
        [Column(TypeName = "decimal(5,1)")]
        public decimal? ApproxProtein { get; set; }
        
        [Column(TypeName = "decimal(5,1)")]
        public decimal? ApproxCarbs { get; set; }
        
        [Column(TypeName = "decimal(5,1)")]
        public decimal? ApproxFats { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Active/Complete status - true means meal is available/complete
        public bool IsComplete { get; set; } = true;
        
        // ✅ Soft delete flag - true means meal is deleted
        public bool IsDeleted { get; set; } = false;
        
        // Image URL for meal photos (stored in Supabase)
        public string? ImageUrl { get; set; }
        
        // Navigation property
        public ICollection<MealOption> MealOptions { get; set; } = new List<MealOption>();
    }
}
