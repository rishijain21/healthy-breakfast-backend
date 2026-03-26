using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace Sovva.Domain.Entities
{
    public class ScheduledOrder
    {
        [Key]
        public int ScheduledOrderId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public Guid AuthId { get; set; } // Supabase auth ID for direct queries

        public string MealName { get; set; } = "Custom Overnight Oats";

        // ✅ Soft reference — nullable so old orders and custom meals aren't broken
        // Do NOT add a FK constraint. MealId is for traceability only.
        public int? MealId { get; set; }

        // ✅ Snapshot — copied at order time so display never depends on Meal record existing
        public string? MealImageUrl { get; set; }
        
        [Column(TypeName = "date")]
        public DateOnly ScheduledFor { get; set; } // Delivery date (date only)
        
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

        // ✅ ADD: Delivery address relationship
        public int? DeliveryAddressId { get; set; }
        public UserAddress? DeliveryAddress { get; set; }

        // ✅ ADD: Subscription relationship
        public int? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public ICollection<ScheduledOrderIngredient> Ingredients { get; set; } = new List<ScheduledOrderIngredient>();
    }
}
