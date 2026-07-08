using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sovva.Domain.Enums;

namespace Sovva.Domain.Entities
{
    public class Order : BaseEntity
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

        public int? Rating { get; set; }
        public string? Review { get; set; }

        public void TransitionTo(OrderStatus newStatus)
        {
            if (OrderStatus == OrderStatus.Cancelled && newStatus != OrderStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot transition from Cancelled state.");
            }
            if (OrderStatus == OrderStatus.Delivered && newStatus != OrderStatus.Delivered)
            {
                throw new InvalidOperationException("Cannot transition from Delivered state.");
            }

            // Allow no-op transition
            if (OrderStatus == newStatus) return;

            // Allow cancellation from any non-terminal state
            if (newStatus == OrderStatus.Cancelled)
            {
                OrderStatus = newStatus;
                return;
            }

            bool isValid = (OrderStatus, newStatus) switch
            {
                (OrderStatus.Pending, OrderStatus.Confirmed) => true,
                (OrderStatus.Confirmed, OrderStatus.Preparing) => true,
                (OrderStatus.Preparing, OrderStatus.OutForDelivery) => true,
                (OrderStatus.OutForDelivery, OrderStatus.Delivered) => true,
                _ => false
            };

            if (!isValid)
            {
                throw new InvalidOperationException($"Invalid order state transition from {OrderStatus} to {newStatus}.");
            }

            OrderStatus = newStatus;
        }

        // Navigation properties
        public User User { get; set; } = null!;
        public UserMeal? UserMeal { get; set; }
        // ✅ NEW: Navigation to source scheduled order (for scheduled order history)
        public ScheduledOrder? SourceScheduledOrder { get; set; }
        public UserAddress? DeliveryAddress { get; set; } // ✅ ADD THIS
    }
}
