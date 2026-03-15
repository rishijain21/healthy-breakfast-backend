using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sovva.Domain.Enums;

namespace Sovva.Domain.Entities
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("UserMeal")]
        public int? UserMealId { get; set; }
        
        // ✅ NEW: Link to source scheduled order (null for real-time orders)
        public int? ScheduledOrderId { get; set; }
        
        public int? DeliveryAddressId { get; set; } // ✅ ADD THIS

        public bool IsPrepared { get; set; } = false;

        [Column("Status")]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
        
        public DateTime OrderDate { get; set; }
        public DateTime ScheduledFor { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public UserMeal? UserMeal { get; set; }
        // ✅ NEW: Navigation to source scheduled order (for scheduled order history)
        public ScheduledOrder? SourceScheduledOrder { get; set; }
        public UserAddress? DeliveryAddress { get; set; } // ✅ ADD THIS
    }
}
