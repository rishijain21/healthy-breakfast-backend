using System;
using System.Collections.Generic;

namespace Sovva.Application.DTOs
{
    public class KitchenOrderDto
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string UserPhoneNumber { get; set; } = string.Empty;
        public string MealName { get; set; } = string.Empty;
        public DateTime ScheduledFor { get; set; }
        public string DeliveryTimeSlot { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public bool IsPrepared { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // ✅ Delivery Address Information
        public KitchenDeliveryAddressDto? DeliveryAddress { get; set; }
        
        // Ingredients needed for preparation
        public List<KitchenIngredientDto> Ingredients { get; set; } = new();
    }

    // ✅ Kitchen-specific Delivery Address DTO (to avoid confusion with UserAddressDto)
    public class KitchenDeliveryAddressDto
    {
        public int AddressId { get; set; }
        public string CompleteAddress { get; set; } = string.Empty;
        
        // ✅ Uses existing ServiceableLocationDto from ServiceableLocationDto.cs
        public ServiceableLocationDto? ServiceableLocation { get; set; }
    }

    public class KitchenIngredientDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Category { get; set; } = string.Empty;
        public string IconEmoji { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
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
