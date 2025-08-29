using System;
using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class IngredientCategory
    {
        [Key]
        public int CategoryId { get; set; }                   // Primary Key

        public string CategoryName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
