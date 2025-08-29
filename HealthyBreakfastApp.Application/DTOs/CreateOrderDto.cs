namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateOrderDto
    {
        public int UserId { get; set; }
        public string OrderStatus { get; set; } = null!;
        public decimal TotalPrice { get; set; }
    }
}
