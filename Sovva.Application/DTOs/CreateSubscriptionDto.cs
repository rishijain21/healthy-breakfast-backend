using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sovva.Domain.Enums;

namespace Sovva.Application.DTOs
{
    public class CreateSubscriptionDto
    {
        // ✅ CHANGED: Use MealId instead of UserMealId
        // The service will auto-find or create UserMeal based on MealId
        
        [Required]
        public int MealId { get; set; }

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
