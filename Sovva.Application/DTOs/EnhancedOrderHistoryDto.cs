using Sovva.Domain.Enums;

namespace Sovva.Application.DTOs
{
    public class EnhancedOrderHistoryDto
    {
        public int OrderId { get; set; }
        public int MealId { get; set; }
        public int UserId { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public string OrderStatusText => OrderStatus.ToString();
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ScheduledFor { get; set; }
        public string? MealName { get; set; }
        
        // Computed nutritional info
        public NutritionalInfoDto NutritionalInfo { get; set; } = new();
        
        // Ingredient breakdown
        public List<OrderIngredientDetailDto> Ingredients { get; set; } = new();
        
        // UI helper properties
        public bool CanReorder => OrderStatus == OrderStatus.Delivered;
        public bool CanRate => OrderStatus == OrderStatus.Delivered;
        public string EstimatedDeliveryTime => ScheduledFor?.ToString("hh:mm tt") ?? "";
    }

    public class NutritionalInfoDto
    {
        public int TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalFiber { get; set; }
    }

    public class OrderIngredientDetailDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string IconEmoji { get; set; } = string.Empty;
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Fiber { get; set; }
    }
}
