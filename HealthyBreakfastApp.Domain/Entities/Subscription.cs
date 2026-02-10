using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthyBreakfastApp.Domain.Enums;

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
        
        public int? DeliveryAddressId { get; set; } // ✅ ADD THIS

        public SubscriptionFrequency Frequency { get; set; }
        
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool Active { get; set; }
        
        public DateOnly? NextScheduledDate { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public UserMeal UserMeal { get; set; } = null!;
        public UserAddress? DeliveryAddress { get; set; } // ✅ ADD THIS
        
        public ICollection<SubscriptionSchedule> WeeklySchedule { get; set; } = new List<SubscriptionSchedule>();
    }
}
