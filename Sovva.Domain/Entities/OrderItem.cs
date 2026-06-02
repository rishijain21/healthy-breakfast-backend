using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class OrderItem : BaseEntity
    {
        [Key]
        public int OrderItemId { get; set; }

        [ForeignKey("Order")]
        public int OrderId { get; set; }

        [ForeignKey("UserMeal")]
        public int UserMealId { get; set; }

        public int Quantity { get; set; }
        public decimal Price { get; set; }

        // Navigation properties
        public Order Order { get; set; } = null!;
        public UserMeal UserMeal { get; set; } = null!;
    }
}
