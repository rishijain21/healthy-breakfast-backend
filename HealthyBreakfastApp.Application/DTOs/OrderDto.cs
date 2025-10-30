using HealthyBreakfastApp.Domain.Enums;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class OrderDto
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        
        // ✅ OPTIMIZED: Use enum instead of string
        public OrderStatus OrderStatus { get; set; }
        
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
