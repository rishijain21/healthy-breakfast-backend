using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateOrderFromMealBuilderDto
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int MealId { get; set; }
        
        [Required]
        public List<SelectedIngredientDto> SelectedIngredients { get; set; } = new();
        
        public DateTime? ScheduledFor { get; set; }
        
        public string? DeliveryAddress { get; set; }
        
        public string? SpecialInstructions { get; set; }
    }

    public class OrderCreationResponseDto
    {
        public int OrderId { get; set; }
        public int UserMealId { get; set; }
        public string MealName { get; set; } = null!;
        public decimal TotalPrice { get; set; }
        public decimal WalletBalanceBefore { get; set; }
        public decimal WalletBalanceAfter { get; set; }
        public string OrderStatus { get; set; } = null!;
        public int TransactionId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ScheduledFor { get; set; }
        public List<IngredientBreakdownDto> IngredientBreakdown { get; set; } = new();
    }

    public class UpdateOrderStatusDto
    {
        [Required]
        public string NewStatus { get; set; } = null!;
        
        public string? StatusReason { get; set; }
    }

    public class CancelOrderResponseDto
    {
        public int OrderId { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal WalletBalanceAfter { get; set; }
        public int RefundTransactionId { get; set; }
        public string CancellationReason { get; set; } = null!;
        public DateTime CancelledAt { get; set; }
    }
}
