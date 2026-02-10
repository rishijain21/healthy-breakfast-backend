namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateOrderDto
    {
        // UserId is extracted from JWT token, not from request body
        public string OrderStatus { get; set; } = null!;
        public decimal TotalPrice { get; set; }
    }
}
