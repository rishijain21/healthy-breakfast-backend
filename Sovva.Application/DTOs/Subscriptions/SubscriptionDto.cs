// Sovva.Application/DTOs/SubscriptionDto.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sovva.Domain.Enums;

namespace Sovva.Application.DTOs
{
    public class SubscriptionDto
    {
        public int SubscriptionId { get; set; }
        public int UserId { get; set; }
        public int? UserMealId { get; set; }
        public decimal AgreedPrice { get; set; }
        public string? PauseReason { get; set; }
        
        /// <summary>
        /// Maps to "mealId" in JSON (frontend expects this name).
        /// Null for UserMeal-based subscriptions where only UserMealId is set.
        /// </summary>
        [JsonPropertyName("mealId")]
        public int? MealId { get; set; }
        
        /// <summary>
        /// Subscription frequency. Serializes as string: "Daily", "Weekly", "Monthly", "Alternate".
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SubscriptionFrequency Frequency { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateOnly? NextScheduledDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public string UserName { get; set; } = null!;
        public string MealName { get; set; } = null!;
        public decimal MealPrice { get; set; }

        /// <summary>
        /// Maps to "imageUrl" in JSON (frontend expects this name).
        /// </summary>
        [JsonPropertyName("imageUrl")]
        public string? MealImageUrl { get; set; }

        // ✅ NEW: Weekly schedule details
        public List<WeeklyScheduleDto> WeeklySchedule { get; set; } = new();

        /// <summary>
        /// Optional warning message (e.g., if first scheduled order creation was skipped)
        /// </summary>
        public string? Warning { get; set; }
    }
}
