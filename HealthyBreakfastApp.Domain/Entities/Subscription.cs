using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class Subscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("UserMeal")]
        public int UserMealId { get; set; }

        public string Frequency { get; set; } = null!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public User User { get; set; } = null!;
        public UserMeal UserMeal { get; set; } = null!;
    }
}
