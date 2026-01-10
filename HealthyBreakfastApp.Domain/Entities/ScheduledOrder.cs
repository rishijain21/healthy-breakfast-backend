using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class ScheduledOrder
    {
        [Key]
        public int ScheduledOrderId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public Guid AuthId { get; set; } // Supabase auth ID for direct queries

        public string MealName { get; set; } = "Custom Overnight Oats";
        
        [Column(TypeName = "date")]
        public DateTime ScheduledFor { get; set; } // Delivery date (date only)
        
        public string DeliveryTimeSlot { get; set; } = "10:00 AM";
        
        public decimal TotalPrice { get; set; }
        
        public string? NutritionalSummary { get; set; } // JSON string for calories, protein, etc.
        
        public string OrderStatus { get; set; } = "scheduled"; // scheduled, confirmed, cancelled
        
        public bool CanModify { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ConfirmedAt { get; set; } // When auto-confirmed at midnight
        
        public DateTime ExpiresAt { get; set; } // Midnight cutoff time

public bool IsProcessedToOrder { get; set; } = false;  // Has been converted to confirmed Order
public int? ConfirmedOrderId { get; set; }             // Link to created Order in Orders table

        // Navigation properties
        public User User { get; set; } = null!;
        public ICollection<ScheduledOrderIngredient> Ingredients { get; set; } = new List<ScheduledOrderIngredient>();
    }
}
