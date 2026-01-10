using System;
using System.Collections.Generic;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class KitchenOrderDto
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public string CustomerName { get; set; }
        public DateTime ScheduledFor { get; set; }
        public string DeliveryTimeSlot { get; set; }
        public decimal TotalPrice { get; set; }
        public bool IsPrepared { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Ingredients needed for preparation
        public List<KitchenIngredientDto> Ingredients { get; set; } = new();
        
        // For grouping by society/delivery route
        public string DeliveryAddress { get; set; }
        public int? SocietyId { get; set; }
        public string SocietyName { get; set; }
    }

    public class KitchenIngredientDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; }
        public int Quantity { get; set; }
        public string Category { get; set; }
        public string IconEmoji { get; set; }
    }

    public class KitchenStatsDto
    {
        public int TotalOrders { get; set; }
        public int PreparedOrders { get; set; }
        public int PendingOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public Dictionary<string, int> IngredientSummary { get; set; } = new();
    }
}
