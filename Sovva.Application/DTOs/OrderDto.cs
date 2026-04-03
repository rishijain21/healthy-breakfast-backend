using Sovva.Domain.Enums;

namespace Sovva.Application.DTOs
{
    public class OrderDto
    {
        public long OrderId { get; set; }
        public int UserId { get; set; }
        
        // ✅ OPTIMIZED: Use enum instead of string
        public OrderStatus OrderStatus { get; set; }
        
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
