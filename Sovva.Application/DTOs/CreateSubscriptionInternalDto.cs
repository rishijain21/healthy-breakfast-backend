using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sovva.Domain.Enums;

namespace Sovva.Application.DTOs
{
    /// <summary>
    /// Internal DTO used by service layer (includes UserId from JWT token)
    /// </summary>
    public class CreateSubscriptionInternalDto
    {
        public int UserId { get; set; }  // Set by controller from JWT

        // ✅ ADD: MealId from frontend (used to auto-find or create UserMeal)
        public int MealId { get; set; }

        [Required]
        public int UserMealId { get; set; }

        [Required]
        public SubscriptionFrequency Frequency { get; set; }

        [Required]
        public DateOnly StartDate { get; set; }

        [Required]
        public DateOnly EndDate { get; set; }

        public bool Active { get; set; } = true;

        public List<WeeklyScheduleDto>? WeeklySchedule { get; set; }
    }
}
