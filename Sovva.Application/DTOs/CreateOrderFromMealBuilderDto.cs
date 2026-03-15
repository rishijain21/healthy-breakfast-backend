using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class CreateOrderFromMealBuilderDto
    {
        public int MealId { get; set; }

        /// <summary>
        /// Snapshot meal name from the scheduled order.
        /// When set, used instead of fetching the meal name from DB.
        /// </summary>
        public string? MealName { get; set; }

        public List<SelectedIngredientDto> SelectedIngredients { get; set; } = new();

        public DateTime? ScheduledFor { get; set; }

        public string? DeliveryAddress { get; set; }

        public string? SpecialInstructions { get; set; }

        /// <summary>
        /// When set by the midnight job, skips live price recalculation and
        /// charges this exact amount — the price the user agreed to at order time.
        /// NEVER set this from a user-facing API endpoint.
        /// </summary>
        public decimal? OverrideTotalPrice { get; set; }
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
