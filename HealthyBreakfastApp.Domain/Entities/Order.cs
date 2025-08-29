using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
  public string OrderStatus { get; set; } = null!;
        public DateTime OrderDate { get; set; }
        public DateTime ScheduledFor { get; set; }
        public decimal TotalPrice { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public User User { get; set; } = null!;
    }
}
