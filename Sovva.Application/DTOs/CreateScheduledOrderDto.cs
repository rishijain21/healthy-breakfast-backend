using System;
using System.Collections.Generic;

namespace Sovva.Application.DTOs
{
    public class CreateScheduledOrderDto
    {
        public string? MealName { get; set; }
        public int? MealId { get; set; }          // ✅ ADD: Soft reference for traceability
        public string? MealImageUrl { get; set; } // ✅ ADD: Snapshot for display
        public decimal? MealPrice { get; set; }
        public List<ScheduledOrderIngredientDto> SelectedIngredients { get; set; } = new();
        public DateTime ScheduledFor { get; set; }
        public string DeliveryTimeSlot { get; set; } = "10:00 AM";
        public int? DeliveryAddressId { get; set; } // ✅ ADD: Delivery address for subscription orders
        public NutritionalSummaryDto? NutritionalSummary { get; set; }
        
        // ✅ ADD: Link to subscription if this order is from a subscription
        public int? SubscriptionId { get; set; }
    }

    public class ScheduledOrderIngredientDto
    {
        public int IngredientId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class NutritionalSummaryDto
    {
        public int TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public int ItemCount { get; set; }
    }
}
