using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthyBreakfastApp.Domain.Enums;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("UserMeal")]
        public int? UserMealId { get; set; }
        
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
        public UserAddress? DeliveryAddress { get; set; } // ✅ ADD THIS
    }
}
