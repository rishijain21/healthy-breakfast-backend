using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sovva.Domain.Enums;
using Sovva.Domain.Interfaces;

namespace Sovva.Domain.Entities
{
    public class Subscription : BaseEntity, ISoftDeletable
    {
        [Key]
        public int SubscriptionId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("UserMeal")]
        public int? UserMealId { get; set; } // Null for fixed meals

        [ForeignKey("Meal")]
        public int? MealId { get; set; } // Null for custom meals

        public decimal AgreedPrice { get; set; }
        
        public string? PauseReason { get; set; }
        
        public int? DeliveryAddressId { get; set; } // ✅ ADD THIS

        public SubscriptionFrequency Frequency { get; set; }
        
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool IsActive { get; set; }
        
        public DateOnly? NextScheduledDate { get; set; }

        /// <summary>Soft delete. Null = active. Has value = cancelled.</summary>
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public UserMeal? UserMeal { get; set; }
        public Meal? Meal { get; set; }
        public UserAddress? DeliveryAddress { get; set; } // ✅ ADD THIS
        
        public ICollection<SubscriptionSchedule> WeeklySchedule { get; set; } = new List<SubscriptionSchedule>();
    }
}
