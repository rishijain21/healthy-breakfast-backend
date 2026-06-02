using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Sovva.Domain.Interfaces;

namespace Sovva.Domain.Entities
{
    public class Meal : BaseEntity, ISoftDeletable
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
        
        // Active/Complete status - true means meal is available/complete
        public bool IsComplete { get; set; } = true;
        
        /// <summary>Soft delete. Null = active. Has value = deleted. Replaces the old IsDeleted bool.</summary>
        public DateTime? DeletedAt { get; set; }
        
        // Image URL for meal photos (stored in Supabase)
        public string? ImageUrl { get; set; }
        
        // Navigation property
        public ICollection<MealOption> MealOptions { get; set; } = new List<MealOption>();
    }
}
