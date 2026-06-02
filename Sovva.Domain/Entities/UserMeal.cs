using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sovva.Domain.Interfaces;

namespace Sovva.Domain.Entities
{
    public class UserMeal : BaseEntity, ISoftDeletable
    {
        [Key]
        public int UserMealId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("Meal")]
        public int MealId { get; set; }

        public string MealName { get; set; } = null!;
        public decimal TotalPrice { get; set; }

        /// <summary>Soft delete. Null = active. Has value = deleted.</summary>
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public Meal Meal { get; set; } = null!;
        
        // ✅ ADD THIS: Collection navigation property for ingredients
        public ICollection<UserMealIngredient> UserMealIngredients { get; set; } = new List<UserMealIngredient>();
    }
}
