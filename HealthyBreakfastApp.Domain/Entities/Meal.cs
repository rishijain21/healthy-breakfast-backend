using System;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class Meal
    {
        public int MealId { get; set; }               // PK: auto-increment
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
