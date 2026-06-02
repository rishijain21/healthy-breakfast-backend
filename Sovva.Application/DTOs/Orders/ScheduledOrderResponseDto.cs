using System;
using System.Collections.Generic;

namespace Sovva.Application.DTOs
{
    public class ScheduledOrderResponseDto
    {
        public int ScheduledOrderId { get; set; }
        public string MealName { get; set; } = null!;
        public int? MealId { get; set; }          // ✅ ADD: Soft reference for traceability
        public string? MealImageUrl { get; set; } // ✅ ADD: Snapshot for display
        public DateTime ScheduledFor { get; set; }
        public string DeliveryTimeSlot { get; set; } = null!;
        public decimal TotalPrice { get; set; }
        public string OrderStatus { get; set; } = null!;
        public bool CanModify { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public NutritionalSummaryDto? NutritionalSummary { get; set; }
        public List<ScheduledOrderIngredientDetailDto> Ingredients { get; set; } = new();
        
        // ✅ ADD: Subscription ID for filtering orders by subscription
        public int? SubscriptionId { get; set; }
    }

    public class ScheduledOrderIngredientDetailDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string Category { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
    }
}
