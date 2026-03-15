// Sovva.Application/DTOs/SubscriptionDto.cs

using System;
using System.Collections.Generic;
using Sovva.Domain.Enums;

namespace Sovva.Application.DTOs
{
    public class SubscriptionDto
    {
        public int SubscriptionId { get; set; }
        public int UserId { get; set; }
        public int UserMealId { get; set; }
        public SubscriptionFrequency Frequency { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool Active { get; set; }
        public DateOnly? NextScheduledDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public string UserName { get; set; } = null!;
        public string MealName { get; set; } = null!;
        public decimal MealPrice { get; set; }

        // ✅ NEW: Weekly schedule details
        public List<WeeklyScheduleDto> WeeklySchedule { get; set; } = new();
    }
}
