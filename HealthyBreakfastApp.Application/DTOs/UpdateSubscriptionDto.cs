// HealthyBreakfastApp.Application/DTOs/UpdateSubscriptionDto.cs

using System.Collections.Generic;
using HealthyBreakfastApp.Domain.Enums;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class UpdateSubscriptionDto
    {
        public SubscriptionFrequency? Frequency { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool? Active { get; set; }
        
        // ✅ NEW: Update weekly schedule
        public List<WeeklyScheduleDto>? WeeklySchedule { get; set; }
    }
}
