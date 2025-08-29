using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class UserMeal
    {
        [Key]
        public int UserMealId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("Meal")]
        public int MealId { get; set; }

        public string MealName { get; set; } = null!;
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public Meal Meal { get; set; } = null!;
    }
}
