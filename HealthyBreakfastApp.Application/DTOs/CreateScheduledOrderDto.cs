using System;
using System.Collections.Generic;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateScheduledOrderDto
    {
        public List<ScheduledOrderIngredientDto> SelectedIngredients { get; set; } = new();
        public DateTime ScheduledFor { get; set; }
        public string DeliveryTimeSlot { get; set; } = "10:00 AM";
        public NutritionalSummaryDto? NutritionalSummary { get; set; }
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
