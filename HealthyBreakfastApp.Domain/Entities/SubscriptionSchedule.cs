// HealthyBreakfastApp.Domain/Entities/SubscriptionSchedule.cs

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    /// <summary>
    /// Stores weekly delivery schedule for subscriptions
    /// Example: Mon=2, Wed=1, Fri=2 (2 bottles Monday, 1 Wednesday, 2 Friday)
    /// </summary>
    public class SubscriptionSchedule
    {
        [Key]
        public int ScheduleId { get; set; }

        [ForeignKey("Subscription")]
        public int SubscriptionId { get; set; }

        // Day of week (0=Sunday, 1=Monday, ..., 6=Saturday)
        public int DayOfWeek { get; set; }

        // Quantity for this day (e.g., 2 bottles)
        public int Quantity { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public Subscription Subscription { get; set; } = null!;
    }
}
