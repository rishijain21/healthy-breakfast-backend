using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class ScheduledOrderIngredient
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("ScheduledOrder")]
        public int ScheduledOrderId { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }

        public int Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ScheduledOrder ScheduledOrder { get; set; } = null!;
        public Ingredient Ingredient { get; set; } = null!;
    }
}
